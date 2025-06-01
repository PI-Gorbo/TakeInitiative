import { validateResponse } from "~/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import { z } from "zod";
import { campaignValidator, type CampaignSettings } from "../../types/models";

// Update Campaign Details

export type UpdateCampaignDetailsRequest = {
    campaignId: string;
    campaignDescription?: string;
    campaignName?: string;
    campaignSettings?: CampaignSettings;
};
const updateCampaignDetailsRequestSchema = campaignValidator;
export type UpdateCampaignResponse = z.infer<
    typeof updateCampaignDetailsRequestSchema
>;
export function updateCampaignDetailsRequest(axios: AxiosInstance) {
    return async function (
        request: UpdateCampaignDetailsRequest
    ): Promise<UpdateCampaignResponse> {
        return await axios
            .put("/api/campaign", request)
            .then((resp) =>
                validateResponse(resp, updateCampaignDetailsRequestSchema)
            );
    };
}
