import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Combat (DMs): a Draft combat in the current session. 409 with no session yet.
export type PostCombatRequest = ApiPathParams<"PostCombat"> &
    ApiRequestBody<"PostCombat">;
export type PostCombatResponse = ApiResponse<"PostCombat">;
export function postCombatRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        ...body
    }: PostCombatRequest): Promise<PostCombatResponse> {
        const response = await axios.post<PostCombatResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats`,
            body satisfies ApiRequestBody<"PostCombat">
        );
        return response.data;
    };
}
