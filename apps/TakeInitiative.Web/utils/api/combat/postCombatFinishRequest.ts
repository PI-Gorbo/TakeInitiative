import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Post Combat Finish (DMs, 18b.5): from Active, or from Draft to discard it.
export type PostCombatFinishRequest = ApiPathParams<"PostCombatFinish">;
export type PostCombatFinishResponse = ApiResponse<"PostCombatFinish">;
export function postCombatFinishRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        combatId,
    }: PostCombatFinishRequest): Promise<PostCombatFinishResponse> {
        // An empty JSON object: FastEndpoints binds a request with no fields from it.
        const response = await axios.post<PostCombatFinishResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats/${encodeURIComponent(combatId)}/finish`,
            {}
        );
        return response.data;
    };
}
