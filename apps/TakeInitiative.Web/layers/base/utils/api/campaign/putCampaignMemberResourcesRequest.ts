import { validateResponse } from "base/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import { z } from "zod";
import {
    campaignMemberValidator,
    campaignValidator,
    type CampaignMember,
    type CampaignMemberResource,
    type UnevaluatedCharacterHealth,
    type UnevaluatedCharacterInitiative,
} from "../../types/models";

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
                return validateResponse(response, campaignMemberValidator);
            });
    };
}
