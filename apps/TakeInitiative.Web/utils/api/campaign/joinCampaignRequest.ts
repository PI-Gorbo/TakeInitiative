import { validateResponse } from "~/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import { z } from "zod";
import { campaignValidator } from "../../types/models";

// Join Campaign

export type JoinCampaignRequest = {
    joinCode: string;
};
const joinCampaignResponseSchema = campaignValidator;
export type JoinCampaignResponse = z.infer<typeof joinCampaignResponseSchema>;
export function joinCampaignRequest(axios: AxiosInstance) {
    return async function (
        request: JoinCampaignRequest
    ): Promise<JoinCampaignResponse> {
        return await axios
            .post("/api/campaign/join", request)
            .then((response) =>
                validateResponse(response, joinCampaignResponseSchema)
            );
    };
}
