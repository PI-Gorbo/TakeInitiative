import { queryOptions, useMutation, useQueryClient, type QueryClient } from "@tanstack/vue-query";
import type { SuggestionMatch, SuggestionSpanRequest } from "~/utils/api/types";
import { batches, MATCH_BATCH_SIZE } from "~/utils/suggestions";
import { invalidateConnections } from "./connections";
import { invalidateLooseEnds } from "./looseEnds";
import { invalidateSessionStreams } from "./sessions";

// Suggestions (23d). Model suggestions are recomputed per device and never cached on the
// server or in the query cache: the page matches the spans it has once the model has looked
// (`useLooseEndSuggestions`), so there is no query here, only the batched call.

/**
 * `POST suggestions/match` for any number of spans, one request per 50 (the API's cap), in
 * order. The answer is aligned with `spans`: an entry the viewer can see, or null.
 */
export async function matchSuggestionSpans(
    api: ReturnType<typeof useApi>,
    campaignId: string,
    spans: readonly SuggestionSpanRequest[]
): Promise<(SuggestionMatch | null)[]> {
    const out: (SuggestionMatch | null)[] = [];
    for (const batch of batches(spans, MATCH_BATCH_SIZE)) {
        const response = await api.suggestion.match({
            campaignId,
            spans: batch,
        });
        out.push(...batch.map((_, i) => response.matches[i] ?? null));
    }
    return out;
}

// Accepted suggestions (23e): the model versions behind the caller's accepted mentions, per
// campaign, and revert. Only the caller's own notes, so nothing here is pushed to others.

export const suggestionModelsKey = (campaignId: string) => ["suggestionModels", campaignId];

/**
 * "gliner_small-v2.5 · 14 mentions in 9 notes" on the Me page, one campaign's. A plain id, not
 * a getter: the Me page asks for every campaign at once with `useQueries`.
 */
export const getSuggestionModelsQuery = (campaignId: string) =>
    queryOptions({
        queryKey: suggestionModelsKey(campaignId),
        queryFn: () => useApi().suggestion.models({ campaignId }),
    });

/** Refetches a campaign's accepted-suggestion counts (a note edit can drop one). */
export const invalidateSuggestionModels = (queryClient: QueryClient, campaignId: string) =>
    queryClient.invalidateQueries({ queryKey: suggestionModelsKey(campaignId) });

/** Revert one model version in the caller's own notes, then re-read what it touched. */
export const revertSuggestionsMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.suggestion.revert,
        onSettled: (_result, _error, { campaignId }) => {
            void invalidateSuggestionModels(queryClient, campaignId);
            void invalidateSessionStreams(queryClient, campaignId);
            void invalidateLooseEnds(queryClient, campaignId);
            void invalidateConnections(queryClient, campaignId);
            void queryClient.invalidateQueries({ queryKey: ["sessionNoteHistory", campaignId] });
        },
    });
};
