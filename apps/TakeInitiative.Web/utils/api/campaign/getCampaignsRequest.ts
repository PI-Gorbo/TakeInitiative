import { validateResponse } from "~/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import { z } from "zod";
import { campaignSummaryValidator } from "../../types/models";

// Get Campaigns: the caller's campaigns
const getCampaignsResponseSchema = z.object({
    campaigns: z.array(campaignSummaryValidator),
});
export type GetCampaignsResponse = z.infer<typeof getCampaignsResponseSchema>;
export function getCampaignsRequest(axios: AxiosInstance) {
    return async function (): Promise<GetCampaignsResponse> {
        return await axios
            .get("/api/campaigns")
            .then((response) =>
                validateResponse(response, getCampaignsResponseSchema)
            );
    };
}
