import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Member Role (owner only)
export type PutMemberRoleRequest = ApiPathParams<"PutMemberRole"> & ApiRequestBody<"PutMemberRole">;
export type PutMemberRoleResponse = ApiResponse<"PutMemberRole">;
export function putMemberRoleRequest(axios: AxiosInstance) {
    return async function ({ campaignId, memberId, ...body }: PutMemberRoleRequest): Promise<PutMemberRoleResponse> {
        const response = await axios.put<PutMemberRoleResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/members/${encodeURIComponent(memberId)}/role`,
            body satisfies ApiRequestBody<"PutMemberRole">
        );
        return response.data;
    };
}
