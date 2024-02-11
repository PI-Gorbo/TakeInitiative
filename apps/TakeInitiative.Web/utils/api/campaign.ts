import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { campaignValidator } from "../types/models";
import { validate } from "vee-validate";

// Create Campaign
export type CreateCampaignRequest = {
    campaignName: string;
};
const createCampaignResponseSchema = campaignValidator;
export type CreateCampaignResponse = yup.InferType<
    typeof createCampaignResponseSchema
>;
function createCampaignRequest(axios: AxiosInstance) {
    return async function createCampaign(
        request: CreateCampaignRequest,
    ): Promise<CreateCampaignResponse> {
        return await axios
            .post("/api/campaign", request)
            .then(async (response) => {
                try {
                    const validateResult =
                        await createCampaignResponseSchema.validate(
                            response.data,
                        );
                    return validateResult;
                } catch (error) {
                    console.log(error);
                    throw error;
                }
            });
    };
}

// Join Campaign
export type JoinCampaignRequest = {
    joinCode: string;
};
const joinCampaignResponseSchema = campaignValidator;
export type JoinCampaignResponse = yup.InferType<
    typeof joinCampaignResponseSchema
>;
function joinCampaignRequest(axios: AxiosInstance) {
    return async function createCampaign(
        request: JoinCampaignRequest,
    ): Promise<JoinCampaignResponse> {
        return await axios
            .post("/api/campaign/join", request)
            .then((response) =>
                joinCampaignResponseSchema.validate(response.data),
            );
    };
}

// Update Campaign Details
export type UpdateCampaignDetailsRequest = {
    campaignId: string;
    campaignDescription: string;
    campaignResources: string;
};
const updateCampaignDetailsRequestSchema = campaignValidator;
export type UpdateCampaignResponse = yup.InferType<
    typeof updateCampaignDetailsRequestSchema
>;
function updateCampaignRequest(axios: AxiosInstance) {
    return async function createCampaign(
        request: UpdateCampaignDetailsRequest,
    ): Promise<UpdateCampaignResponse> {
        return await axios
            .put("/api/campaign", request)
            .then((response) =>
                updateCampaignDetailsRequestSchema.validate(response.data),
            );
    };
}

export const campaign = (axios: AxiosInstance) => ({
    create: createCampaignRequest(axios),
    join: joinCampaignRequest(axios),
    update: updateCampaignRequest(axios),
});
