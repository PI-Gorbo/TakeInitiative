import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Campaign (members only)
export type GetCampaignRequest = ApiPathParams<"GetCampaign">;
export type GetCampaignResponse = ApiResponse<"GetCampaign">;
export function getCampaignRequest(axios: AxiosInstance) {
    return async function (request: GetCampaignRequest): Promise<GetCampaignResponse> {
        const response = await axios.get<GetCampaignResponse>(
            `/api/campaigns/${encodeURIComponent(request.campaignId)}`
        );
        return response.data;
    };
}
