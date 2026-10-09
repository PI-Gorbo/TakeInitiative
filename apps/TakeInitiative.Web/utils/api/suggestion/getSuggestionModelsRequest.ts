import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Suggestion Models (members, 23c): which model versions suggested mentions the caller
// accepted in their own notes, with how many mentions in how many notes. Only the caller's.
export type GetSuggestionModelsRequest = ApiPathParams<"GetSuggestionModels">;
export type GetSuggestionModelsResponse = ApiResponse<"GetSuggestionModels">;
export function getSuggestionModelsRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
    }: GetSuggestionModelsRequest): Promise<GetSuggestionModelsResponse> {
        const response = await axios.get<GetSuggestionModelsResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/suggestions/models`
        );
        return response.data;
    };
}
