import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Campaign Name (DMs only)
export type PutCampaignNameRequest = ApiPathParams<"PutCampaignName"> & ApiRequestBody<"PutCampaignName">;
export type PutCampaignNameResponse = ApiResponse<"PutCampaignName">;
export function putCampaignNameRequest(axios: AxiosInstance) {
    return async function ({ campaignId, ...body }: PutCampaignNameRequest): Promise<PutCampaignNameResponse> {
        const response = await axios.put<PutCampaignNameResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/name`,
            body satisfies ApiRequestBody<"PutCampaignName">
        );
        return response.data;
    };
}
