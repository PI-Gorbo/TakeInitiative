import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { campaignValidator } from "../../types/models";

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
export function updateCampaignDetailsRequest(axios: AxiosInstance) {
	return async function (
		request: UpdateCampaignDetailsRequest
	): Promise<UpdateCampaignResponse> {
		return await axios
			.put("/api/campaign", request)
			.then((response) => {
				return updateCampaignDetailsRequestSchema.validate(response.data);
			});
	};
}
