import { validateResponse } from "~/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import type { z } from "zod";
import { campaignValidator, type Role } from "../../types/models";

// Put Member Role (owner only)
export type PutMemberRoleRequest = {
    campaignId: string;
    memberId: string;
    role: Role;
};
export type PutMemberRoleResponse = z.infer<typeof campaignValidator>;
export function putMemberRoleRequest(axios: AxiosInstance) {
    return async function (
        request: PutMemberRoleRequest
    ): Promise<PutMemberRoleResponse> {
        return await axios
            .put(
                `/api/campaigns/${encodeURIComponent(request.campaignId)}/members/${encodeURIComponent(request.memberId)}/role`,
                { role: request.role }
            )
            .then((response) => validateResponse(response, campaignValidator));
    };
}
