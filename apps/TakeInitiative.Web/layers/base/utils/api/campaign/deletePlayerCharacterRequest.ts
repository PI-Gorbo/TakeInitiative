import { validateResponse } from "base/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import {
    campaignMemberValidator,
    type CampaignMember,
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
                return validateResponse(response, campaignMemberValidator);
            });
    };
}
