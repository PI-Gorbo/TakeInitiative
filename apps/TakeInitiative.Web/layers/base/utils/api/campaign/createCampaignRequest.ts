import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { campaignValidator } from "../../types/models";

// Create Campaign
export type CreateCampaignRequest = {
    campaignName: string;
};
const createCampaignResponseSchema = campaignValidator;
export type CreateCampaignResponse = yup.InferType<
    typeof createCampaignResponseSchema
>;
export function createCampaignRequest(axios: AxiosInstance) {
    return async function (
        request: CreateCampaignRequest,
    ): Promise<CreateCampaignResponse> {
        return await axios
            .post("/api/campaign", request)
            .then(async (response) =>
                validateWithSchema(response.data, createCampaignResponseSchema),
            );
    };
}
