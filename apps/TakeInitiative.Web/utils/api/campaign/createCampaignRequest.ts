import { validateResponse } from "~/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import type { z } from "zod";
import { campaignValidator } from "../../types/models";

// Create Campaign
export type CreateCampaignRequest = {
    name: string;
};
export type CreateCampaignResponse = z.infer<typeof campaignValidator>;
export function createCampaignRequest(axios: AxiosInstance) {
    return async function (
        request: CreateCampaignRequest
    ): Promise<CreateCampaignResponse> {
        return await axios
            .post("/api/campaigns", request)
            .then(async (response) =>
                validateResponse(response, campaignValidator)
            );
    };
}
