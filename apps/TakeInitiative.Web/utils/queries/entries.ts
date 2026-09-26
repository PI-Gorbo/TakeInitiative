import {
    infiniteQueryOptions,
    queryOptions,
    useMutation,
    useQuery,
    useQueryClient,
    type QueryClient,
} from "@tanstack/vue-query";
import type { Entry, EntryList, EntrySummary, EntryTimeline, SessionNote } from "~/utils/api/types";
import { apiErrorStatus } from "~/utils/apiErrorParser";
import { entryDirectory } from "~/utils/entries";
import {
    mergeEntrySummary,
    removeEntry,
    timelineTouchedBy,
    upsertEntrySummary,
    type EntryTimelineData,
} from "~/utils/entryCache";
import type { RefOrGetter } from "./utils";

/** Timeline notes per page. The server allows 1–50. */
export const TIMELINE_PAGE_SIZE = 20;

// ── The wiki list and the entry directory ────────────────────────────────────

export const getEntriesQueryKey = (campaignId: MaybeRefOrGetter<string | null>) => ["entries", campaignId];

/**
 * Every entry the viewer can see, with the viewer's mention counts: the wiki list and
 * the entry directory that mention chips and 15d's `@` read. Pushes keep the entries
 * fresh; the counts are never pushed (15b), so the list is also read again on window
 * focus, when the Wiki tab mounts, and after a hub join.
 */
export const getEntriesQuery = (campaignId: RefOrGetter<string | null>) =>
    queryOptions({
        queryKey: getEntriesQueryKey(campaignId),
        queryFn: () => useApi().entry.list({ campaignId: toValue(campaignId)! }),
        enabled: () => !!toValue(campaignId),
        staleTime: Infinity,
        refetchOnWindowFocus: "always",
    });

/**
 * The viewer's entry directory for a campaign (`utils/entries.ts`), shared by every
 * caller through the `["entries", campaignId]` query. Empty until the list loads.
 */
export function useEntryDirectory(campaignId: RefOrGetter<string | null>) {
    const query = useQuery(getEntriesQuery(campaignId));
    return computed(() => entryDirectory(query.data.value));
}

// ── One entry and its timeline ───────────────────────────────────────────────

export const getEntryQueryKey = (campaignId: MaybeRefOrGetter<string>, entryId: MaybeRefOrGetter<string>) => [
    "entry",
    campaignId,
    entryId,
];

/** Not retried: a 404 means the entry is not there or the viewer cannot see it. */
const retryUnless404 = (failureCount: number, error: unknown) => apiErrorStatus(error) !== 404 && failureCount < 3;

export const getEntryQuery = (campaignId: RefOrGetter<string>, entryId: RefOrGetter<string>) =>
    queryOptions({
        queryKey: getEntryQueryKey(campaignId, entryId),
        queryFn: () =>
            useApi().entry.get({
                campaignId: toValue(campaignId),
                entryId: toValue(entryId),
            }),
        enabled: () => !!toValue(campaignId) && !!toValue(entryId),
        // Pushes keep it fresh; a reconnect or a role change invalidates it.
        staleTime: Infinity,
        retry: retryUnless404,
    });

const timelinesKey = (campaignId: string) => ["entryTimeline", campaignId];
export const getEntryTimelineQueryKey = (
    campaignId: MaybeRefOrGetter<string>,
    entryId: MaybeRefOrGetter<string>
) => ["entryTimeline", campaignId, entryId];

/**
 * An entry's timeline, newest page first. Items are oldest first within a page; the
 * next page is the notes posted before the oldest one loaded.
 */
export const getEntryTimelineQuery = (campaignId: RefOrGetter<string>, entryId: RefOrGetter<string>) =>
    infiniteQueryOptions({
        queryKey: getEntryTimelineQueryKey(campaignId, entryId),
        queryFn: ({ pageParam }) =>
            useApi().entry.timeline({
                campaignId: toValue(campaignId),
                entryId: toValue(entryId),
                before: pageParam,
                take: TIMELINE_PAGE_SIZE,
            }),
        initialPageParam: undefined as string | undefined,
        getNextPageParam: (lastPage: EntryTimeline) =>
            lastPage.hasOlder && lastPage.items.length > 0 ? lastPage.items[0].note.postedAt : undefined,
        enabled: () => !!toValue(campaignId) && !!toValue(entryId),
        // A note push invalidates the timelines it touches (`invalidateTimelinesTouchedBy`).
        staleTime: Infinity,
        retry: retryUnless404,
    });

// ── Applying pushes and responses (the hub and the mutations share these) ────

/** An entry push or response: the list (keeping its counts) and the loaded entry. */
export function applyEntrySummary(queryClient: QueryClient, campaignId: string, summary: EntrySummary) {
    queryClient.setQueryData<EntryList>(getEntriesQueryKey(campaignId), (list) => upsertEntrySummary(list, summary));
    for (const query of queryClient.getQueryCache().findAll({ queryKey: ["entry", campaignId] })) {
        queryClient.setQueryData<Entry>(query.queryKey, (entry) => mergeEntrySummary(entry, summary));
    }
}

/**
 * New entries a post is creating (15d): added to the list unless a push already did,
 * so a real entry is never replaced by its placeholder.
 */
export function addPendingEntries(queryClient: QueryClient, campaignId: string, summaries: readonly EntrySummary[]) {
    if (summaries.length === 0) return;
    queryClient.setQueryData<EntryList>(getEntriesQueryKey(campaignId), (list) =>
        summaries.reduce(
            (acc, summary) =>
                acc?.entries.some((i) => i.entry.id.toLowerCase() === summary.id.toLowerCase())
                    ? acc
                    : upsertEntrySummary(acc, summary),
            list
        )
    );
}

/** A failed post's new entries leave the list again. */
export function removePendingEntries(queryClient: QueryClient, campaignId: string, entries: readonly { id: string }[]) {
    for (const { id } of entries) {
        queryClient.setQueryData<EntryList>(getEntriesQueryKey(campaignId), (list) => removeEntry(list, id));
    }
}

/**
 * `entryRemoved`: the viewer can no longer see the entry. It leaves the list, and an
 * open entry page reads it again and gets its 404.
 */
export function applyEntryRemoved(queryClient: QueryClient, campaignId: string, entryId: string) {
    queryClient.setQueryData<EntryList>(getEntriesQueryKey(campaignId), (list) => removeEntry(list, entryId));
    void queryClient.invalidateQueries({
        predicate: (query) =>
            (query.queryKey[0] === "entry" || query.queryKey[0] === "entryTimeline") &&
            query.queryKey[1] === campaignId &&
            String(query.queryKey[2]).toLowerCase() === entryId.toLowerCase(),
    });
}

/**
 * A note was posted, edited, moved or removed: refetch the loaded timelines it can
 * change (`timelineTouchedBy`). The note's own push already reaches exactly its
 * audience, so nothing else is pushed for timelines (15b).
 */
export function invalidateTimelinesTouchedBy(
    queryClient: QueryClient,
    campaignId: string,
    note: Pick<SessionNote, "id"> & Partial<Pick<SessionNote, "text">>
) {
    const queries = queryClient.getQueryCache().findAll({ queryKey: timelinesKey(campaignId) });
    const touched = new Set(
        timelineTouchedBy(
            note,
            queries.map((q) => ({ entryId: String(q.queryKey[2]), data: q.state.data as EntryTimelineData | undefined }))
        )
    );
    for (const query of queries) {
        if (touched.has(String(query.queryKey[2]).toLowerCase())) {
            void queryClient.invalidateQueries({
                queryKey: query.queryKey,
                exact: true,
            });
        }
    }
}

/**
 * `entryArticleChanged` (15e): the viewer's view of an article changed. The payload
 * has no content, so a loaded entry is read again. An open article editor keeps its
 * own copy of the text and shows "This article changed" (`ArticleEditor`). Timelines
 * list which articles mention their entry, so the loaded ones are read again too.
 */
export function applyEntryArticleChanged(queryClient: QueryClient, campaignId: string, entryId: string) {
    void queryClient.invalidateQueries({
        predicate: (query) =>
            query.queryKey[0] === "entry" &&
            query.queryKey[1] === campaignId &&
            String(query.queryKey[2]).toLowerCase() === entryId.toLowerCase(),
    });
    void queryClient.invalidateQueries({ queryKey: timelinesKey(campaignId) });
}

/** Everything entry-related for a campaign: after a hub join, a reconnect, or a role change. */
export function invalidateEntries(queryClient: QueryClient, campaignId: string) {
    return queryClient.invalidateQueries({
        predicate: (query) =>
            ["entries", "entry", "entryTimeline"].includes(String(query.queryKey[0])) && query.queryKey[1] === campaignId,
    });
}

// ── Mutations ─────────────────────────────────────────────────────────────────

/** A write's response: the list, and the entry itself (it is the whole entry). */
export function applyEntryResponse(queryClient: QueryClient, campaignId: string, entry: Entry) {
    applyEntrySummary(queryClient, campaignId, entry);
    queryClient.setQueryData(getEntryQueryKey(campaignId, entry.id), (old: Entry | undefined) =>
        old ? { ...old, ...entry } : entry
    );
}

// Any member: create an entry. A duplicate name is a 409 with `errors.existingEntryId`.
export const createEntryMutation = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: useApi().entry.create,
        onSuccess: (entry, { campaignId }) => applyEntryResponse(queryClient, campaignId, entry),
    });
};

// Can edit: name, kind, aliases.
export const putEntryNameMutation = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: useApi().entry.putName,
        onSuccess: (entry, { campaignId }) => applyEntryResponse(queryClient, campaignId, entry),
    });
};
export const putEntryKindMutation = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: useApi().entry.putKind,
        onSuccess: (entry, { campaignId }) => applyEntryResponse(queryClient, campaignId, entry),
    });
};
export const putEntryAliasesMutation = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: useApi().entry.putAliases,
        onSuccess: (entry, { campaignId }) => applyEntryResponse(queryClient, campaignId, entry),
    });
};

// The creator and DMs: visibility and edit access.
export const putEntryVisibilityMutation = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: useApi().entry.putVisibility,
        onSuccess: (entry, { campaignId }) => applyEntryResponse(queryClient, campaignId, entry),
    });
};
export const putEntryEditAccessMutation = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: useApi().entry.putEditAccess,
        onSuccess: (entry, { campaignId }) => applyEntryResponse(queryClient, campaignId, entry),
    });
};

// Can edit: the article (15f). The body is the caller's whole view, in order, with the
// etag it was loaded with; the response is the entry as the caller now sees it.
export const putEntryArticleMutation = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: useApi().entry.putArticle,
        onSuccess: (entry, { campaignId }) => {
            applyEntryResponse(queryClient, campaignId, entry);
            // Article mentions show on timelines and in the counts (15e).
            void queryClient.invalidateQueries({
                queryKey: timelinesKey(campaignId),
            });
            void queryClient.invalidateQueries({
                queryKey: getEntriesQueryKey(campaignId),
            });
        },
    });
};

// Can edit the entry, and can see the note: promote a note, or part of it (15f).
export const promoteNoteMutation = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: useApi().entry.promote,
        onSuccess: ({ entry }, { campaignId }) => {
            applyEntryResponse(queryClient, campaignId, entry);
            void queryClient.invalidateQueries({
                queryKey: timelinesKey(campaignId),
            });
        },
    });
};
