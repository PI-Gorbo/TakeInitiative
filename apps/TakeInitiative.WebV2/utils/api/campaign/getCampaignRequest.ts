import { validateResponse } from "~/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import { z } from "zod";
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

const campaignMemberDtoValidator = z
    .object({
        userId: z.string(),
        username: z.string(),
        isDungeonMaster: z.boolean(),
        resources: z.array(campaignMemberResourceValidator),
    })
    .required();

const combatDtoValidator = z
    .object({
        id: z.string(),
        state: z.nativeEnum(CombatState),
        combatName: z.string(),
        dungeonMaster: z.string(),
        currentPlayers: z.array(playerDtoValidator),
    })
    .required();

const finishedCombatDtoValidator = z
    .object({
        combatId: z.string(),
        name: z.string(),
    })
    .required();
export type FinishedCombatDto = z.infer<typeof finishedCombatDtoValidator>;

export type CombatDto = z.infer<typeof combatDtoValidator>;
export type CampaignMemberDto = z.infer<typeof campaignMemberDtoValidator>;
const getCampaignResponseSchema = z
    .object({
        campaign: campaignValidator,
        userCampaignMember: campaignMemberValidator,
        nonUserCampaignMembers: z.array(campaignMemberDtoValidator),
        joinCode: z.string(),
        currentCombatInfo: combatDtoValidator.nullable(),
        combatHistory: z.array(
            z
                .object({
                    combatId: z.string().uuid(),
                    combatName: z.string(),
                    finishedOn: z.string(),
                })
                .required()
        ),
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
