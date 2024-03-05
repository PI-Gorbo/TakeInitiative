import type { AxiosInstance } from "axios";
import * as yup from "yup";
import {
    campaignMemberValidator,
    campaignValidator,
    plannedCombatValidator,
    playerCharacterValidator,
} from "../../types/models";

// Create Campaign
export type GetCampaignRequest = {
    campaignId: string;
};

const campaignMemberDtoValidator = yup.object({
    userId: yup.string().required(),
    username: yup.string().required(),
    isDungeonMaster: yup.boolean().required(),
    currentCharacter: playerCharacterValidator.nullable(),
});
export type CampaignMemberDto = yup.InferType<
    typeof campaignMemberDtoValidator
>;
const getCampaignResponseSchema = yup.object({
    campaign: campaignValidator,
    userCampaignMember: campaignMemberValidator,
    nonUserCampaignMembers: yup.array(campaignMemberDtoValidator),
    plannedCombats: yup.array(plannedCombatValidator).nullable(),
	joinCode: yup.string().required(),
});
export type GetCampaignResponse = yup.InferType<
    typeof getCampaignResponseSchema
>;
export function getCampaignRequest(axios: AxiosInstance) {
    return async function (
        request: GetCampaignRequest,
    ): Promise<GetCampaignResponse> {
        return await axios
            .get(`/api/campaign/${encodeURI(request.campaignId)}`)
            .then(async (response) =>
                validateWithSchema(response.data, getCampaignResponseSchema),
            );
    };
}
