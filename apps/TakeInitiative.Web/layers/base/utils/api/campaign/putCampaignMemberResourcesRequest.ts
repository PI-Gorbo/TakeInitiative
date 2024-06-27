import type { AxiosInstance } from "axios";
import * as yup from "yup";
import {
    campaignMemberValidator,
    campaignValidator,
    type CampaignMember,
    type CampaignMemberResource,
    type CharacterHealth,
    type CharacterInitiative,
} from "../../types/models";
import type { PlayerCharacterDto } from "./createPlayerCharacterRequest";

export type PutCampaignMemberResourcesRequest = {
    memberId: string;
    resources: CampaignMemberResource[];
};

export function putCampaignMemberResourcesRequest(axios: AxiosInstance) {
    return async function (
        request: PutCampaignMemberResourcesRequest,
    ): Promise<CampaignMember> {
        return await axios
            .put("/api/campaign/member/resources", request)
            .then(async (response) => {
                return validateWithSchema(
                    response.data,
                    campaignMemberValidator,
                );
            });
    };
}
