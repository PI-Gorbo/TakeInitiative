import {
    infiniteQueryOptions,
    queryOptions,
    useMutation,
    useQueryClient,
    type QueryClient,
} from "@tanstack/vue-query";
import type {
    Campaign,
    Session,
    SessionList,
    SessionNote,
    SessionStream,
    SessionStreamFilter,
    Visibility,
} from "~/utils/api/types";
import type { SessionStreamData } from "~/utils/sessionStreamCache";
import { removeNote, upsertNote, upsertSession, upsertSessionInList } from "~/utils/sessionStreamCache";
import { getCampaignQueryKey } from "./campaign";
import { invalidateTimelinesTouchedBy } from "./entries";
import type { RefOrGetter } from "./utils";

/** Sessions per page. The server allows 1–10. */
export const SESSION_STREAM_PAGE_SIZE = 3;

// Kept apart from `["campaign", id]` so a member joining does not refetch the stream.
export const sessionStreamsKey = (campaignId: MaybeRefOrGetter<string | null>) => ["sessionStream", campaignId];
export const getSessionStreamQueryKey = (
    campaignId: MaybeRefOrGetter<string | null>,
    filter: MaybeRefOrGetter<SessionStreamFilter>
) => ["sessionStream", campaignId, filter];

/**
 * The session stream, one page of sessions at a time. The first page ends at the
 * current session; the next page is the sessions numbered below the oldest one
 * loaded.
 */
export const getSessionStreamQuery = (
    campaignId: RefOrGetter<string | null>,
    filter: RefOrGetter<SessionStreamFilter>
) =>
    infiniteQueryOptions({
        queryKey: getSessionStreamQueryKey(campaignId, filter),
        queryFn: ({ pageParam }) =>
            useApi().session.getStream({
                campaignId: toValue(campaignId)!,
                filter: toValue(filter),
                before: pageParam,
                take: SESSION_STREAM_PAGE_SIZE,
            }),
        initialPageParam: undefined as number | undefined,
        getNextPageParam: (lastPage: SessionStream) =>
            lastPage.hasOlder && lastPage.sessions.length > 0
                ? Math.min(...lastPage.sessions.map((s) => s.session.number))
                : undefined,
        enabled: () => !!toValue(campaignId),
        // Pushes keep it fresh; a reconnect or a role change invalidates it.
        staleTime: Infinity,
    });

/**
 * Applies a cache update to every loaded stream of a campaign (one per filter).
 * `update` is one of the pure functions in `utils/sessionStreamCache.ts`.
 */
export function updateSessionStreams(
    queryClient: QueryClient,
    campaignId: string,
    update: (data: SessionStreamData, filter: SessionStreamFilter, currentMemberId: string | undefined) => SessionStreamData | undefined
) {
    const currentMemberId = queryClient.getQueryData<Campaign>(getCampaignQueryKey(campaignId))?.currentMemberId;
    for (const query of queryClient.getQueryCache().findAll({ queryKey: sessionStreamsKey(campaignId) })) {
        const filter = query.queryKey[2] as SessionStreamFilter;
        queryClient.setQueryData<SessionStreamData>(query.queryKey, (data) =>
            data ? update(data, filter, currentMemberId) : data
        );
    }
}

/** Refetches every loaded stream of a campaign. */
export const invalidateSessionStreams = (queryClient: QueryClient, campaignId: string) =>
    queryClient.invalidateQueries({ queryKey: sessionStreamsKey(campaignId) });

// ── Sessions list (the composer's session picker and gap prompt) ─────────────

export const getSessionsQueryKey = (campaignId: MaybeRefOrGetter<string | null>) => ["sessions", campaignId];

/** Every session, newest first, with `currentSessionId` and the gap prompt flag. */
export const getSessionsQuery = (campaignId: RefOrGetter<string | null>) =>
    queryOptions({
        queryKey: getSessionsQueryKey(campaignId),
        queryFn: () => useApi().session.list({ campaignId: toValue(campaignId)! }),
        enabled: () => !!toValue(campaignId),
        // Pushes keep the sessions fresh; the gap prompt is re-read after every post.
        staleTime: Infinity,
    });

/** Applies a session push or response to the sessions list and every loaded stream. */
export function applySession(queryClient: QueryClient, campaignId: string, session: Session) {
    queryClient.setQueryData<SessionList>(getSessionsQueryKey(campaignId), (data) => upsertSessionInList(data, session));
    updateSessionStreams(queryClient, campaignId, (data) => upsertSession(data, session));
}

/** Re-reads the sessions list, and with it the gap prompt. */
export const invalidateSessions = (queryClient: QueryClient, campaignId: string) =>
    queryClient.invalidateQueries({ queryKey: getSessionsQueryKey(campaignId) });

// DMs: set or clear a session's title.
export const putSessionTitleMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.session.putTitle,
        onSuccess: (session, { campaignId }) => applySession(queryClient, campaignId, session),
    });
};

// Any member: start the next session. `number` is current + 1; when someone else has
// just started it, the API returns theirs.
export const startSessionMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.session.start,
        onSuccess: (session, { campaignId }) => applySession(queryClient, campaignId, session),
        // A 409 means the list is behind (someone started two sessions); read it again.
        onError: (_error, { campaignId }) => invalidateSessions(queryClient, campaignId),
    });
};

// ── Notes ─────────────────────────────────────────────────────────────────────

/**
 * Applies a note response to every loaded stream, keyed by note id, like a push, and
 * refetches the loaded timelines it touches (15c).
 */
const applyNote = (queryClient: QueryClient, campaignId: string, note: SessionNote) => {
    updateSessionStreams(queryClient, campaignId, (data, filter, me) => upsertNote(data, note, filter, me));
    invalidateTimelinesTouchedBy(queryClient, campaignId, note);
};

export type PostNoteVariables = {
    campaignId: string;
    body: { sessionId?: string; text: string; visibility: Visibility; isRecap: boolean };
    /** Shown at once under a temporary id and replaced by the response. */
    optimistic?: SessionNote;
};

// Any member: post a note. Optimistic; the push and the response are the same note
// id, so whichever lands second is a no-op.
export const postNoteMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ campaignId, body }: PostNoteVariables) => api.note.post({ campaignId, ...body }),
        onMutate: ({ campaignId, optimistic }) => {
            if (optimistic) applyNote(queryClient, campaignId, optimistic);
        },
        onSuccess: (note, { campaignId, optimistic }) => {
            updateSessionStreams(queryClient, campaignId, (data, filter, me) =>
                upsertNote(optimistic ? removeNote(data, optimistic.id) : data, note, filter, me)
            );
            invalidateTimelinesTouchedBy(queryClient, campaignId, note);
        },
        onError: (_error, { campaignId, optimistic }) => {
            if (optimistic) updateSessionStreams(queryClient, campaignId, (data) => removeNote(data, optimistic.id));
        },
        // The gap prompt re-reads after every post.
        onSettled: (_note, _error, { campaignId }) => invalidateSessions(queryClient, campaignId),
    });
};

export const getNoteHistoryQueryKey = (campaignId: MaybeRefOrGetter<string>, noteId: MaybeRefOrGetter<string>) => [
    "sessionNoteHistory",
    campaignId,
    noteId,
];

/** A note's versions, oldest first. */
export const getNoteHistoryQuery = (campaignId: RefOrGetter<string>, noteId: RefOrGetter<string>) =>
    queryOptions({
        queryKey: getNoteHistoryQueryKey(campaignId, noteId),
        queryFn: () => useApi().note.history({ campaignId: toValue(campaignId), noteId: toValue(noteId) }),
    });

// Author: edit the text and the recap flag.
export const putNoteMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.note.put,
        onSuccess: (note, { campaignId }) => {
            applyNote(queryClient, campaignId, note);
            void queryClient.invalidateQueries({ queryKey: getNoteHistoryQueryKey(campaignId, note.id) });
        },
    });
};

// Author: change who can see the note.
export const putNoteVisibilityMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.note.putVisibility,
        onSuccess: (note, { campaignId }) => applyNote(queryClient, campaignId, note),
    });
};

// DMs: hide or unhide a note.
export const putNoteHiddenMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.note.putHidden,
        onSuccess: (note, { campaignId }) => applyNote(queryClient, campaignId, note),
    });
};

// Author: delete a note.
export const deleteNoteMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.note.delete,
        onSuccess: (_void, { campaignId, noteId }) => {
            updateSessionStreams(queryClient, campaignId, (data) => removeNote(data, noteId));
            invalidateTimelinesTouchedBy(queryClient, campaignId, { id: noteId });
        },
    });
};
