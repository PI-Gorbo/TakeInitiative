import {
    queryOptions,
    useMutation,
    useQueryClient,
    type QueryClient,
} from "@tanstack/vue-query";
import type {
    Combat,
    CombatList,
    CombatStatus,
    CombatSummary,
} from "~/utils/api/types";
import { apiErrorStatus } from "~/utils/apiErrorParser";
import { summaryFromCombat, upsertCombatSummary } from "~/utils/combat";
import type { RefOrGetter } from "./utils";

// Combat v2 (18c). Every write answers with the caller's own `CombatResponse` and the
// hub pushes `combatChanged` to everyone else, so both land in the cache and the page
// never refetches on its own writes. A reconnect or a role change refetches both.

// ── The list ─────────────────────────────────────────────────────────────────

/** Every loaded list of a campaign, one per status filter. */
export const combatListsKey = (campaignId: MaybeRefOrGetter<string | null>) => [
    "combats",
    campaignId,
];
/** The status filter is the key's third part: `"Active"`, `"Draft,Finished"` or `"all"`. */
export const getCombatsQueryKey = (
    campaignId: MaybeRefOrGetter<string | null>,
    status?: readonly CombatStatus[]
) => [
    "combats",
    campaignId,
    status && status.length > 0 ? status.join(",") : "all",
];

/**
 * The combats the viewer can see, newest first: the Combat tab reads them all; 18e's
 * banner reads `{ status: ["Active"] }`. Pushes keep every loaded list current.
 */
export const getCombatsQuery = (
    campaignId: RefOrGetter<string | null>,
    options: { status?: readonly CombatStatus[] } = {}
) =>
    queryOptions({
        queryKey: getCombatsQueryKey(campaignId, options.status),
        queryFn: () =>
            useApi().combat.list({
                campaignId: toValue(campaignId)!,
                status: options.status,
            }),
        enabled: () => !!toValue(campaignId),
        staleTime: Infinity,
    });

// ── One combat ───────────────────────────────────────────────────────────────

const combatsKey = (campaignId: string) => ["combat", campaignId];
export const getCombatQueryKey = (
    campaignId: MaybeRefOrGetter<string>,
    combatId: MaybeRefOrGetter<string>
) => ["combat", campaignId, combatId];

/** Not retried: a 404 means the combat is not there, or not for the viewer (a Draft). */
const retryUnless404 = (failureCount: number, error: unknown) =>
    apiErrorStatus(error) !== 404 && failureCount < 3;

export const getCombatQuery = (
    campaignId: RefOrGetter<string>,
    combatId: RefOrGetter<string>
) =>
    queryOptions({
        queryKey: getCombatQueryKey(campaignId, combatId),
        queryFn: () =>
            useApi().combat.get({
                campaignId: toValue(campaignId),
                combatId: toValue(combatId),
            }),
        enabled: () => !!toValue(campaignId) && !!toValue(combatId),
        staleTime: Infinity,
        retry: retryUnless404,
    });

// ── Applying pushes and responses ────────────────────────────────────────────

/** The status filter a list query was read with (its key's third part). */
const listStatuses = (queryKey: readonly unknown[]) =>
    typeof queryKey[2] === "string" && queryKey[2] !== "all"
        ? (queryKey[2].split(",") as CombatStatus[])
        : undefined;

/** A summary into every loaded list of the campaign. */
export function applyCombatSummary(
    queryClient: QueryClient,
    campaignId: string,
    summary: CombatSummary
) {
    for (const query of queryClient
        .getQueryCache()
        .findAll({ queryKey: combatListsKey(campaignId) })) {
        queryClient.setQueryData<CombatList>(query.queryKey, (list) =>
            upsertCombatSummary(list, summary, listStatuses(query.queryKey))
        );
    }
}

/** `combatChanged`: the receiver's own view of the combat, and its summary. */
export function applyCombatChanged(
    queryClient: QueryClient,
    campaignId: string,
    message: { combat: Combat; summary: CombatSummary }
) {
    queryClient.setQueryData<Combat>(
        getCombatQueryKey(campaignId, message.combat.id),
        message.combat
    );
    applyCombatSummary(queryClient, campaignId, message.summary);
}

/**
 * A write's response. The loaded combat takes it; the lists take a summary built from
 * the one they hold, or are read again when there is none (a new combat, or a first
 * roll that moved it to another session).
 */
export function applyCombatResponse(
    queryClient: QueryClient,
    campaignId: string,
    combat: Combat
) {
    queryClient.setQueryData<Combat>(
        getCombatQueryKey(campaignId, combat.id),
        combat
    );
    const previous = queryClient
        .getQueriesData<CombatList>({ queryKey: combatListsKey(campaignId) })
        .flatMap(([, list]) => list?.combats ?? [])
        .find((c) => c.id.toLowerCase() === combat.id.toLowerCase());
    const summary = summaryFromCombat(combat, previous);
    if (summary) applyCombatSummary(queryClient, campaignId, summary);
    else
        void queryClient.invalidateQueries({
            queryKey: combatListsKey(campaignId),
        });
}

/** A reconnect or a role change: what the viewer may see may have changed. */
export function invalidateCombats(
    queryClient: QueryClient,
    campaignId: string
) {
    void queryClient.invalidateQueries({
        queryKey: combatListsKey(campaignId),
    });
    void queryClient.invalidateQueries({ queryKey: combatsKey(campaignId) });
}

// ── Mutations, one per endpoint ──────────────────────────────────────────────

function combatMutation<TArgs extends { campaignId: string }>(
    call: (args: TArgs) => Promise<Combat>
) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: call,
        onSuccess: (combat, { campaignId }) =>
            applyCombatResponse(queryClient, campaignId, combat),
    });
}

export const createCombatMutation = () =>
    combatMutation(useApi().combat.create);
export const addCombatantsMutation = () =>
    combatMutation(useApi().combat.addCombatants);
export const putCombatantMutation = () =>
    combatMutation(useApi().combat.putCombatant);
export const deleteCombatantMutation = () =>
    combatMutation(useApi().combat.deleteCombatant);
export const rollCombatMutation = () => combatMutation(useApi().combat.roll);
export const finishCombatMutation = () =>
    combatMutation(useApi().combat.finish);
export const putCombatantPositionMutation = () =>
    combatMutation(useApi().combat.putPosition);

/**
 * End turn. A 409 means the turn already moved on (a double tap, or two DMs at once):
 * that is done, not an error, and the push that moved it is already here or on its way.
 */
export const endTurnMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: Parameters<typeof api.combat.endTurn>[0]) => {
            try {
                return await api.combat.endTurn(args);
            } catch (err) {
                if (apiErrorStatus(err) === 409) return null;
                throw err;
            }
        },
        onSuccess: (combat, { campaignId, combatId }) => {
            if (combat) applyCombatResponse(queryClient, campaignId, combat);
            else
                void queryClient.invalidateQueries({
                    queryKey: getCombatQueryKey(campaignId, combatId),
                });
        },
    });
};

// ── History (18d reads it) ───────────────────────────────────────────────────

export const getCombatHistoryQuery = (
    campaignId: RefOrGetter<string>,
    combatId: RefOrGetter<string>
) =>
    queryOptions({
        queryKey: ["combatHistory", campaignId, combatId],
        queryFn: () =>
            useApi().combat.history({
                campaignId: toValue(campaignId),
                combatId: toValue(combatId),
            }),
        enabled: () => !!toValue(campaignId) && !!toValue(combatId),
    });
