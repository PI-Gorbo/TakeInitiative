import { validateResponse } from "base/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import { z } from "zod";
import {
    campaignMemberValidator,
    campaignValidator,
    type CampaignMember,
    type CharacterHealth,
    type CharacterInitiative,
} from "../../types/models";

export type PlayerCharacterDto = {
    name: string;
    health: CharacterHealth | null;
    initiative: CharacterInitiative | null;
    armourClass: number | null;
};
export type CreatePlayerCharacterRequest = {
    campaignMemberId: string;
    playerCharacter: PlayerCharacterDto;
};

export function createPlayerCharacterRequest(axios: AxiosInstance) {
    return async function (
        request: CreatePlayerCharacterRequest,
    ): Promise<CampaignMember> {
        return await axios
            .post("/api/campaign/member/character", request)
            .then(async (response) => {
                return validateResponse(response, campaignMemberValidator);
            });
    };
}
