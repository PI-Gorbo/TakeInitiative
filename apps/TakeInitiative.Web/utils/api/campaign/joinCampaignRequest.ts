import type { AxiosInstance } from "axios";
import type { ApiRequestBody, ApiResponse } from "../types";

// Join Campaign (by join code, as a Player)
export type JoinCampaignRequest = ApiRequestBody<"PostJoinCampaign">;
export type JoinCampaignResponse = ApiResponse<"PostJoinCampaign">;
export function joinCampaignRequest(axios: AxiosInstance) {
    return async function (request: JoinCampaignRequest): Promise<JoinCampaignResponse> {
        const response = await axios.post<JoinCampaignResponse>("/api/campaigns/join", request);
        return response.data;
    };
}
