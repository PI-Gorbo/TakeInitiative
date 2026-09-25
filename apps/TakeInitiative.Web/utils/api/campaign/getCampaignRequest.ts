import { validateResponse } from "~/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import type { z } from "zod";
import { campaignValidator } from "../../types/models";

// Get Campaign (members only)
export type GetCampaignRequest = {
    campaignId: string;
};
export type GetCampaignResponse = z.infer<typeof campaignValidator>;
export function getCampaignRequest(axios: AxiosInstance) {
    return async function (
        request: GetCampaignRequest
    ): Promise<GetCampaignResponse> {
        return await axios
            .get(`/api/campaigns/${encodeURIComponent(request.campaignId)}`)
            .then(async (response) =>
                validateResponse(response, campaignValidator)
            );
    };
}
