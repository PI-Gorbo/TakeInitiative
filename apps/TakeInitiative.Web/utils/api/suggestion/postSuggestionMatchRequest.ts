import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Suggestion Match (members, 23c): which of the model's spans are existing entries the
// caller can see, in request order (null for none). At most 50 spans a request. Only the span
// strings are sent, never the note.
export type PostSuggestionMatchRequest = ApiPathParams<"PostSuggestionMatch"> &
    ApiRequestBody<"PostSuggestionMatch">;
export type PostSuggestionMatchResponse = ApiResponse<"PostSuggestionMatch">;
export function postSuggestionMatchRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        ...body
    }: PostSuggestionMatchRequest): Promise<PostSuggestionMatchResponse> {
        const response = await axios.post<PostSuggestionMatchResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/suggestions/match`,
            body satisfies ApiRequestBody<"PostSuggestionMatch">
        );
        return response.data;
    };
}
