import {
    keepPreviousData,
    queryOptions,
    type QueryClient,
} from "@tanstack/vue-query";
import { allKinds, type GraphView } from "~/utils/graph";
import { apiErrorStatus } from "~/utils/apiErrorParser";
import type { RefOrGetter } from "./utils";

// Connections (19c). They are derived per viewer on every read and never pushed
// (19, Notes "No pushes of their own"): `useCampaignHub` invalidates the whole prefix
// on every push that can move one (notes, entries, articles, merges, combats), and
// the refetch applies the viewer's own rules.

/** Every connection query of a campaign: an entry's panel and each pair's evidence. */
export const connectionsKey = (campaignId: MaybeRefOrGetter<string | null>) => [
    "connections",
    campaignId,
];

/** Not retried: a 404 means the entry is not there, or not for the viewer. */
const retryUnless404 = (failureCount: number, error: unknown) =>
    apiErrorStatus(error) !== 404 && failureCount < 3;

/** The entries connected to this one, heaviest first (the Connections panel). */
export const getEntryConnectionsQuery = (
    campaignId: RefOrGetter<string>,
    entryId: RefOrGetter<string>
) =>
    queryOptions({
        queryKey: ["connections", campaignId, entryId],
        queryFn: () =>
            useApi().connection.forEntry({
                campaignId: toValue(campaignId),
                entryId: toValue(entryId),
            }),
        enabled: () => !!toValue(campaignId) && !!toValue(entryId),
        retry: retryUnless404,
    });

/** The evidence for one pair (the evidence sheet), read only while the sheet is open. */
export const getConnectionEvidenceQuery = (
    campaignId: RefOrGetter<string>,
    entryId: RefOrGetter<string>,
    otherEntryId: RefOrGetter<string>,
    enabled: RefOrGetter<boolean> = () => true
) =>
    queryOptions({
        queryKey: [
            "connections",
            campaignId,
            entryId,
            "evidence",
            otherEntryId,
        ],
        queryFn: () =>
            useApi().connection.evidence({
                campaignId: toValue(campaignId),
                entryId: toValue(entryId),
                otherEntryId: toValue(otherEntryId),
            }),
        enabled: () =>
            toValue(enabled) &&
            !!toValue(campaignId) &&
            !!toValue(entryId) &&
            !!toValue(otherEntryId),
        retry: retryUnless404,
    });

/**
 * The graph page's nodes and edges (19d) for one view (focus, depth, kinds). Keyed
 * under the same prefix, so the pushes that refetch the panel refetch the graph. The
 * last graph stays drawn while a new view loads, so a chip does not blank the page.
 */
export const getConnectionGraphQuery = (
    campaignId: RefOrGetter<string>,
    view: RefOrGetter<GraphView>
) =>
    queryOptions({
        queryKey: [
            "connections",
            campaignId,
            "graph",
            () => toValue(view).focus,
            () => (toValue(view).focus ? toValue(view).depth : null),
            () => toValue(view).kinds.join(","),
        ],
        queryFn: () => {
            const { focus, depth, kinds } = toValue(view);
            return useApi().connection.graph({
                campaignId: toValue(campaignId),
                focus,
                depth,
                kinds: allKinds(kinds) ? undefined : kinds,
            });
        },
        enabled: () => !!toValue(campaignId),
        placeholderData: keepPreviousData,
        retry: retryUnless404,
    });

/** Refetches every connection query of the campaign that is on screen. */
export const invalidateConnections = (
    queryClient: QueryClient,
    campaignId: string
) => queryClient.invalidateQueries({ queryKey: connectionsKey(campaignId) });
