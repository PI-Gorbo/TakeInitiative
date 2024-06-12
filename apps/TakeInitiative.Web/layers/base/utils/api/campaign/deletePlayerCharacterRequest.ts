import type { AxiosInstance } from "axios";
import * as yup from "yup";
import {
    campaignMemberValidator,
    campaignValidator,
    type CampaignMember,
    type CharacterHealth,
    type CharacterInitiative,
} from "../../types/models";

export type DeletePlayerCharacterRequest = {
    memberId: string;
    playerCharacterId: string;
};

export function deletePlayerCharacterRequest(axios: AxiosInstance) {
    return async function (
        request: DeletePlayerCharacterRequest,
    ): Promise<CampaignMember> {
        return await axios
            .delete("/api/campaign/member/character", { data: request })
            .then(async (response) => {
                return validateWithSchema(
                    response.data,
                    campaignMemberValidator,
                );
            });
    };
}
