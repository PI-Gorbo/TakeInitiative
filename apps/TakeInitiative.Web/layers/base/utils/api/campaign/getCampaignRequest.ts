import type { AxiosInstance } from "axios";
import * as yup from "yup";
import {
    CombatState,
    campaignMemberResourceValidator,
    campaignMemberValidator,
    campaignValidator,
    plannedCombatValidator,
    playerCharacterValidator,
    playerDtoValidator,
} from "../../types/models";

// Create Campaign
export type GetCampaignRequest = {
    campaignId: string;
};

const campaignMemberDtoValidator = yup.object({
    userId: yup.string().required(),
    username: yup.string().required(),
    isDungeonMaster: yup.boolean().required(),
    resources: yup.array(campaignMemberResourceValidator),
});

const combatDtoValidator = yup.object({
    id: yup.string().required(),
    state: yup.mixed().oneOf(Object.values(CombatState)).required(),
    combatName: yup.string().required(),
    dungeonMaster: yup.string().required(),
    currentPlayers: yup.array(playerDtoValidator).required(),
});

const finishedCombatDtoValidator = yup.object({
    combatId: yup.string().required(),
    name: yup.string().required(),
});
export type FinishedCombatDto = yup.InferType<
    typeof finishedCombatDtoValidator
>;

export type CombatDto = yup.InferType<typeof combatDtoValidator>;
export type CampaignMemberDto = yup.InferType<
    typeof campaignMemberDtoValidator
>;
const getCampaignResponseSchema = yup.object({
    campaign: campaignValidator,
    userCampaignMember: campaignMemberValidator,
    nonUserCampaignMembers: yup.array(campaignMemberDtoValidator),
    joinCode: yup.string().required(),
    currentCombatInfo: combatDtoValidator.nullable(),
    combatHistoryInfo: yup.object({
        lastCombatTimestamp: yup.string().nullable(),
        totalCombats: yup.number(),
    }),
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
            )
            .then((resp) => {
                return resp;
            });
    };
}
