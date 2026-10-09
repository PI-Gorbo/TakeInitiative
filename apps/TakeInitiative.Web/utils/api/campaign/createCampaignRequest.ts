import type { AxiosInstance } from "axios";
import type { ApiRequestBody, ApiResponse } from "../types";

// Create Campaign
export type CreateCampaignRequest = ApiRequestBody<"PostCreateCampaign">;
export type CreateCampaignResponse = ApiResponse<"PostCreateCampaign">;
export function createCampaignRequest(axios: AxiosInstance) {
    return async function (request: CreateCampaignRequest): Promise<CreateCampaignResponse> {
        const response = await axios.post<CreateCampaignResponse>("/api/campaigns", request);
        return response.data;
    };
}
