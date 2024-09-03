import { validateResponse } from "base/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import {
    campaignMemberValidator,
    type CampaignMember,
} from "../../types/models";
import type { PlayerCharacterDto } from "./createPlayerCharacterRequest";

export type UpdatePlayerCharacterRequest = {
    campaignMemberId: string;
    playerCharacterId: string;
    playerCharacter: PlayerCharacterDto;
};

export function updatePlayerCharacterRequest(axios: AxiosInstance) {
    return async function (
        request: UpdatePlayerCharacterRequest,
    ): Promise<CampaignMember> {
        return await axios
            .put("/api/campaign/member/character", request)
            .then(async (response) => {
                return validateResponse(response, campaignMemberValidator);
            });
    };
}
