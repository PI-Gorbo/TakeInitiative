import * as signalR from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/vue-query";
import { toast } from "vue-sonner";
import type { Campaign, Role, Session, SessionNote } from "~/utils/api/types";
import { getCampaignQueryKey, getCampaignsQueryKey } from "~/utils/queries/campaign";
import { invalidateSessionStreams, updateSessionStreams } from "~/utils/queries/sessions";
import { removeNote, upsertNote, upsertSession } from "~/utils/sessionStreamCache";

// Payloads of `CampaignHubMessages` (the API's CampaignHub.cs and SessionHub.cs).
type MemberRoleChangedMessage = { campaignId: string; memberId: string; role: Role };
type SessionNoteRemovedMessage = { noteId: string; sessionId: string };
type SessionNoteHiddenMessage = { noteId: string; sessionId: string; byMemberId: string };

/**
 * Keeps the open campaign live over `CampaignHub`: joins the `campaign:{id}` group
 * for the given campaign, refreshes the campaign query when a member joins or the
 * owner changes a role, and applies session and note pushes to the session stream
 * cache. Call it once, from the campaign layout.
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
        void queryClient.invalidateQueries({ queryKey: getCampaignsQueryKey() });
        // A new role changes which notes the caller can see (DM notes, hidden notes).
        if (id && message?.memberId === me) void invalidateSessionStreams(queryClient, id);
    });

    // Session stream pushes (14b). They are sent after the save and are idempotent:
    // a repeat is a no-op, keyed by session or note id.
    const updateStreams = (update: Parameters<typeof updateSessionStreams>[2]) => {
        const id = joinedCampaignId.value;
        if (id) updateSessionStreams(queryClient, id, update);
    };
    connection.on("sessionStarted", (session: Session) => {
        updateStreams((data) => upsertSession(data, session));
    });
    connection.on("sessionTitleChanged", (session: Session) => {
        updateStreams((data) => upsertSession(data, session));
    });
    connection.on("sessionNoteUpserted", (note: SessionNote) => {
        updateStreams((data, filter, me) => upsertNote(data, note, filter, me));
    });
    connection.on("sessionNoteRemoved", ({ noteId }: SessionNoteRemovedMessage) => {
        updateStreams((data) => removeNote(data, noteId));
    });
    connection.on("sessionNoteHidden", (_message: SessionNoteHiddenMessage) => {
        // Sent to the author only. The note itself arrives as an upsert with `isHidden`.
        toast.info("A DM hid your note. You can still see it.", { duration: 6000 });
    });

    connection.onreconnected(async () => {
        // A reconnect leaves every group, so join again and catch up on anything
        // pushed while the connection was down.
        const id = joinedCampaignId.value;
        if (id) {
            await connection.invoke("Join", id);
            void invalidateSessionStreams(queryClient, id);
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
