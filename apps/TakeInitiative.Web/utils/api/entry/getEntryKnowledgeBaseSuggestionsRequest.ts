import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Entry Knowledge-base Suggestions (28b): the corpus rows this entry might be, at most three,
// best first. **Empty is the normal answer** — most entries look like nothing in the corpus — and a
// caller who may not see suggestions gets the same empty list rather than a 403, so there is one
// code path here and nothing is revealed by a status code. A 404 still means "no such entry".
export type GetEntryKnowledgeBaseSuggestionsRequest = ApiPathParams<"GetEntryKnowledgeBaseSuggestions">;
export type GetEntryKnowledgeBaseSuggestionsResponse = ApiResponse<"GetEntryKnowledgeBaseSuggestions">;
export function getEntryKnowledgeBaseSuggestionsRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        entryId,
    }: GetEntryKnowledgeBaseSuggestionsRequest): Promise<GetEntryKnowledgeBaseSuggestionsResponse> {
        const response = await axios.get<GetEntryKnowledgeBaseSuggestionsResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/knowledge-base-suggestions`
        );
        return response.data;
    };
}
