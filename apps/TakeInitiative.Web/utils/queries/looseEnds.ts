import { queryOptions, type QueryClient } from "@tanstack/vue-query";
import type { RefOrGetter } from "./utils";

// Loose ends (19e). Like connections they are derived per viewer on every read and
// never pushed (19, Notes "No pushes of their own"): `useCampaignHub` invalidates the
// whole prefix on the same pushes as connections, and the refetch applies the
// viewer's own rules, so a count drops by itself when its note gets a mention.

/** Every loose-end query of a campaign: the counts and the list. */
export const looseEndsKey = (campaignId: MaybeRefOrGetter<string | null>) => ["looseEnds", campaignId];

/** The Wiki's "Loose ends (n)", the dividers' 🧵 n and ⌘K's action. Cheap: no matcher call. */
export const getLooseEndCountsQuery = (campaignId: RefOrGetter<string>) =>
    queryOptions({
        queryKey: ["looseEnds", campaignId, "counts"],
        queryFn: () => useApi().looseEnd.counts({ campaignId: toValue(campaignId) }),
        enabled: () => !!toValue(campaignId),
    });

/**
 * The viewer's loose ends, with link suggestions (the loose-ends page). One list for
 * the campaign: the page narrows it to a session itself, so "All" is already loaded.
 */
export const getLooseEndsQuery = (campaignId: RefOrGetter<string>) =>
    queryOptions({
        queryKey: ["looseEnds", campaignId, "list"],
        queryFn: () => useApi().looseEnd.list({ campaignId: toValue(campaignId) }),
        enabled: () => !!toValue(campaignId),
    });

/** Refetches every loose-end query of the campaign that is on screen. */
export const invalidateLooseEnds = (queryClient: QueryClient, campaignId: string) =>
    queryClient.invalidateQueries({ queryKey: looseEndsKey(campaignId) });
