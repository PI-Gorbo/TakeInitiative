import { validateResponse } from "base/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import { z } from "zod";
import { campaignValidator } from "../../types/models";

// Create Campaign
export type CreateCampaignRequest = {
    campaignName: string;
};
const createCampaignResponseSchema = campaignValidator;
export type CreateCampaignResponse = z.infer<
    typeof createCampaignResponseSchema
>;
export function createCampaignRequest(axios: AxiosInstance) {
    return async function (
        request: CreateCampaignRequest,
    ): Promise<CreateCampaignResponse> {
        return await axios
            .post("/api/campaign", request)
            .then(async (response) =>
                validateResponse(response, createCampaignResponseSchema),
            );
    };
}
