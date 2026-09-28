import type { SuggestionMatch, SuggestionSpanRequest } from "~/utils/api/types";
import { batches, MATCH_BATCH_SIZE } from "~/utils/suggestions";

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
