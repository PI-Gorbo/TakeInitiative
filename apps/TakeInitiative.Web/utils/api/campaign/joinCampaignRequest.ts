import { validateResponse } from "~/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import type { z } from "zod";
import { campaignValidator } from "../../types/models";

// Join Campaign (by join code, as a Player)
export type JoinCampaignRequest = {
    joinCode: string;
};
export type JoinCampaignResponse = z.infer<typeof campaignValidator>;
export function joinCampaignRequest(axios: AxiosInstance) {
    return async function (
        request: JoinCampaignRequest
    ): Promise<JoinCampaignResponse> {
        return await axios
            .post("/api/campaigns/join", request)
            .then((response) => validateResponse(response, campaignValidator));
    };
}
