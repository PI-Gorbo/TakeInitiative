import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Suggestion Revert (members, 23c): unlinks the mentions one model version suggested in
// the caller's own notes, as the caller's plain edits. Hand-typed mentions stay; entries created
// from its suggestions are kept and listed.
export type PostSuggestionRevertRequest =
    ApiPathParams<"PostSuggestionRevert"> &
        ApiRequestBody<"PostSuggestionRevert">;
export type PostSuggestionRevertResponse = ApiResponse<"PostSuggestionRevert">;
export function postSuggestionRevertRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        ...body
    }: PostSuggestionRevertRequest): Promise<PostSuggestionRevertResponse> {
        const response = await axios.post<PostSuggestionRevertResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/suggestions/revert`,
            body satisfies ApiRequestBody<"PostSuggestionRevert">
        );
        return response.data;
    };
}
