import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { campaignValidator } from "../../types/models";

// Create Campaign
export type GetCampaignRequest = {
	campaignId: string;
};
const getCampaignResponseSchema = campaignValidator;
export type GetCampaignResponse = yup.InferType<
	typeof getCampaignResponseSchema
>;
export function getCampaignRequest(axios: AxiosInstance) {
	return async function (
		request: GetCampaignRequest
	): Promise<GetCampaignResponse> {
		return await axios
			.get(`/api/campaign/${encodeURI(request.campaignId)}`)
			.then(async (response) => validateWithSchema(response.data, getCampaignResponseSchema));
	};
}
