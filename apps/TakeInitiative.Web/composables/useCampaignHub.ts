import * as signalR from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/vue-query";
import { toast } from "vue-sonner";
import type { Campaign, EntrySummary, Role, Session, SessionNote } from "~/utils/api/types";
import { getCampaignQueryKey, getCampaignsQueryKey } from "~/utils/queries/campaign";
import {
    applyEntryArticleChanged,
    applyEntryMerged,
    applyEntryStatsChanged,
    applyEntryRemoved,
    applyEntrySummary,
    invalidateEntries,
} from "~/utils/queries/entries";
import {
    applySession,
    invalidateNoteViews,
    invalidateSessions,
    invalidateSessionStreams,
    updateSessionStreams,
} from "~/utils/queries/sessions";
import type { TouchingNote } from "~/utils/gallery";
import { dropPendingCopy, removeNote, upsertNote } from "~/utils/sessionStreamCache";

// Payloads of `CampaignHubMessages` (the API's CampaignHub.cs and SessionHub.cs).
type MemberRoleChangedMessage = {
    campaignId: string;
    memberId: string;
    role: Role;
};
type SessionNoteRemovedMessage = { noteId: string; sessionId: string };
type SessionNoteHiddenMessage = {
    noteId: string;
    sessionId: string;
    byMemberId: string;
};
// Payloads of the entry messages (the API's EntryHub.cs).
type EntryRemovedMessage = { entryId: string };
type EntryArticleChangedMessage = { entryId: string };
type EntryMergedMessage = { fromEntryId: string; intoEntryId: string };
type EntryStatsChangedMessage = { entryId: string };

/**
 * Keeps the open campaign live over `CampaignHub`: joins the `campaign:{id}` group
 * for the given campaign, refreshes the campaign query when a member joins or the
 * owner changes a role, and applies session and note pushes to the session stream
 * cache and entry pushes to the wiki (15c). Call it once, from the campaign layout.
 */
export function useCampaignHub(campaignId: MaybeRefOrGetter<string | undefined>) {
    const queryClient = useQueryClient();
    const joinedCampaignId = ref<string | null>(null);

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${useRuntimeConfig().public.axios.baseURL}/campaignHub`, {
            accessTokenFactory: () => useCookie(".AspNetCore.Cookies").value!,
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    const refreshCampaign = () =>
        queryClient.invalidateQueries({
            queryKey: getCampaignQueryKey(joinedCampaignId.value),
        });

    // Message names are `CampaignHubMessages` in the API. Handlers must return
    // nothing: SignalR logs an error when a handler returns a value (a promise too).
    connection.on("memberJoined", () => {
        void refreshCampaign();
    });
    connection.on("memberRoleChanged", (message: MemberRoleChangedMessage) => {
        const id = joinedCampaignId.value;
        const me = id ? queryClient.getQueryData<Campaign>(getCampaignQueryKey(id))?.currentMemberId : undefined;
        void refreshCampaign();
        // The caller's own role may have changed; the campaign list shows it.
        void queryClient.invalidateQueries({
            queryKey: getCampaignsQueryKey(),
        });
        // A new role changes which notes the caller can see (DM notes, hidden notes).
        if (id && message?.memberId === me) {
            void invalidateSessionStreams(queryClient, id);
            void invalidateSessions(queryClient, id);
            void invalidateEntries(queryClient, id);
        }
    });

    // Session stream pushes (14b). They are sent after the save and are idempotent:
    // a repeat is a no-op, keyed by session or note id.
    const updateStreams = (update: Parameters<typeof updateSessionStreams>[2]) => {
        const id = joinedCampaignId.value;
        if (id) updateSessionStreams(queryClient, id, update);
    };
    // A session change also updates the composer's session picker (14d).
    const updateSession = (session: Session) => {
        const id = joinedCampaignId.value;
        if (id) applySession(queryClient, id, session);
    };
    connection.on("sessionStarted", (session: Session) => {
        updateSession(session);
    });
    connection.on("sessionTitleChanged", (session: Session) => {
        updateSession(session);
    });
    // A note change can change the timelines of the entries it mentions (15c), and the
    // galleries of its session and of those entries when it has, or had, images (16d).
    const touchNoteViews = (note: TouchingNote) => {
        const id = joinedCampaignId.value;
        if (id) invalidateNoteViews(queryClient, id, note);
    };
    connection.on("sessionNoteUpserted", (note: SessionNote) => {
        // The caller's own post can arrive before its POST answers: drop the optimistic copy.
        updateStreams((data, filter, me) => upsertNote(dropPendingCopy(data, note), note, filter, me));
        touchNoteViews(note);
    });
    connection.on("sessionNoteRemoved", ({ noteId, sessionId }: SessionNoteRemovedMessage) => {
        updateStreams((data) => removeNote(data, noteId));
        touchNoteViews({ id: noteId, sessionId });
    });
    connection.on("sessionNoteHidden", (_message: SessionNoteHiddenMessage) => {
        // Sent to the author only. The note itself arrives as an upsert with `isHidden`.
        toast.info("A DM hid your note. You can still see it.", {
            duration: 6000,
        });
    });

    // Entry pushes (15a). `entryUpserted` is a bare summary: mention counts are per
    // viewer and never pushed, so the list keeps the counts it has.
    connection.on("entryUpserted", (entry: EntrySummary) => {
        const id = joinedCampaignId.value;
        if (id) applyEntrySummary(queryClient, id, entry);
    });
    connection.on("entryRemoved", ({ entryId }: EntryRemovedMessage) => {
        const id = joinedCampaignId.value;
        if (id) applyEntryRemoved(queryClient, id, entryId);
    });

    // Sent only to members whose view of the article changed (15e), with no content.
    connection.on("entryArticleChanged", ({ entryId }: EntryArticleChangedMessage) => {
        const id = joinedCampaignId.value;
        if (id) applyEntryArticleChanged(queryClient, id, entryId);
    });

    // Merge (15g): sent to the target's audience, who could all see the merged entry.
    // The target's `entryUpserted` follows; a merged entry the viewer can no longer see
    // arrives as `entryRemoved` instead.
    connection.on("entryMerged", ({ fromEntryId, intoEntryId }: EntryMergedMessage) => {
        const id = joinedCampaignId.value;
        if (id) applyEntryMerged(queryClient, id, fromEntryId, intoEntryId);
    });
    // Sent only to members whose readable stats changed (15g), with no content.
    connection.on("entryStatsChanged", ({ entryId }: EntryStatsChangedMessage) => {
        const id = joinedCampaignId.value;
        if (id) applyEntryStatsChanged(queryClient, id, entryId);
    });

    connection.onreconnected(async () => {
        // A reconnect leaves every group, so join again and catch up on anything
        // pushed while the connection was down.
        const id = joinedCampaignId.value;
        if (id) {
            await connection.invoke("Join", id);
            void invalidateSessionStreams(queryClient, id);
            void invalidateSessions(queryClient, id);
            void invalidateEntries(queryClient, id);
        }
        await refreshCampaign();
    });

    async function join(id: string) {
        if (connection.state === signalR.HubConnectionState.Disconnected) {
            await connection.start();
        }
        await connection.invoke("Join", id);
        joinedCampaignId.value = id;
        // A stream fetched before the join finished can miss a note pushed in between.
        void invalidateSessionStreams(queryClient, id);
        void invalidateSessions(queryClient, id);
        // The wiki list too, and with it the mention counts, which are never pushed.
        void invalidateEntries(queryClient, id);
    }

    async function leave() {
        const id = joinedCampaignId.value;
        joinedCampaignId.value = null;
        if (id && connection.state === signalR.HubConnectionState.Connected) {
            await connection.invoke("Leave", id);
        }
    }

    watch(
        () => toValue(campaignId),
        async (id) => {
            if (!id || id === joinedCampaignId.value) return;
            try {
                await leave();
                await join(id);
            } catch (err) {
                console.warn("Could not join the campaign hub.", err);
            }
        },
        { immediate: true }
    );

    onScopeDispose(async () => {
        await leave().catch(() => {});
        await connection.stop();
    });

    return { connection };
}
