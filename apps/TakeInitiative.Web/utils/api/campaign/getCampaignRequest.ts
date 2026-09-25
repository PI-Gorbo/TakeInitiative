import { validateResponse } from "~/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import { z } from "zod";
import {
    campaignMemberValidator,
    campaignValidator,
} from "../../types/models";

// Create Campaign
export type GetCampaignRequest = {
    campaignId: string;
};

const campaignMemberDtoValidator = z
    .object({
        userId: z.string(),
        username: z.string(),
    })
    .required();

export type CampaignMemberDto = z.infer<typeof campaignMemberDtoValidator>;
const getCampaignResponseSchema = z
    .object({
        campaign: campaignValidator,
        userCampaignMember: campaignMemberValidator,
        campaignMembers: z.array(campaignMemberDtoValidator),
        joinCode: z.string(),
    })
    .required();
export type GetCampaignResponse = z.infer<typeof getCampaignResponseSchema>;
export function getCampaignRequest(axios: AxiosInstance) {
    return async function (
        request: GetCampaignRequest
    ): Promise<GetCampaignResponse> {
        return await axios
            .get(`/api/campaign/${encodeURI(request.campaignId)}`)
            .then(async (response) =>
                validateResponse(response, getCampaignResponseSchema)
            );
    };
}
