import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { campaignValidator } from "../../types/models";

// Join Campaign

export type JoinCampaignRequest = {
	joinCode: string;
};
const joinCampaignResponseSchema = campaignValidator;
export type JoinCampaignResponse = yup.InferType<
	typeof joinCampaignResponseSchema
>;
export function joinCampaignRequest(axios: AxiosInstance) {
	return async function (
		request: JoinCampaignRequest
	): Promise<JoinCampaignResponse> {
		return await axios
			.post("/api/campaign/join", request)
			.then((response) => joinCampaignResponseSchema.validate(response.data)
			);
	};
}
