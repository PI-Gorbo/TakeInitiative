import {
    infiniteQueryOptions,
    useMutation,
    useQueryClient,
    type QueryClient,
} from "@tanstack/vue-query";
import type { Campaign, SessionStream, SessionStreamFilter } from "~/utils/api/types";
import type { SessionStreamData } from "~/utils/sessionStreamCache";
import { upsertSession } from "~/utils/sessionStreamCache";
import { getCampaignQueryKey } from "./campaign";
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

// DMs: set or clear a session's title.
export const putSessionTitleMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.session.putTitle,
        onSuccess: (session, { campaignId }) => {
            updateSessionStreams(queryClient, campaignId, (data) => upsertSession(data, session));
        },
    });
};
