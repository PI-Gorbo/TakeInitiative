import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Entry Suggestion Dismiss (28b): "no, this entry is not that row". Idempotent, authorised as
// the links write path (403 for a member who may not edit), and it answers the suggestions that are
// left — so the same shape the GET answers. The dismissal is campaign-wide: it is a fact about the
// entry, not a per-member preference, so the other DM is not asked the same question again.
export type PostEntrySuggestionDismissRequest = ApiPathParams<"PostEntrySuggestionDismiss"> &
    ApiRequestBody<"PostEntrySuggestionDismiss">;
export type PostEntrySuggestionDismissResponse = ApiResponse<"PostEntrySuggestionDismiss">;
export function postEntrySuggestionDismissRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        entryId,
        ...body
    }: PostEntrySuggestionDismissRequest): Promise<PostEntrySuggestionDismissResponse> {
        const response = await axios.post<PostEntrySuggestionDismissResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/knowledge-base-suggestions/dismiss`,
            body satisfies ApiRequestBody<"PostEntrySuggestionDismiss">
        );
        return response.data;
    };
}
