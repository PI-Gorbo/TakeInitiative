import type { AxiosInstance } from "axios";
import type { ApiResponse } from "../types";

// Get Campaigns: the caller's campaigns
export type GetCampaignsResponse = ApiResponse<"GetCampaigns">;
export function getCampaignsRequest(axios: AxiosInstance) {
    return async function (): Promise<GetCampaignsResponse> {
        const response = await axios.get<GetCampaignsResponse>("/api/campaigns");
        return response.data;
    };
}
