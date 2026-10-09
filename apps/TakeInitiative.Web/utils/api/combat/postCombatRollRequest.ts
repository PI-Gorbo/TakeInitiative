import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Post Combat Roll (18b.1): a DM rolls every waiting combatant, and the first roll starts the
// combat. A player rolls only their own, in an Active combat.
export type PostCombatRollRequest = ApiPathParams<"PostCombatRoll">;
export type PostCombatRollResponse = ApiResponse<"PostCombatRoll">;
export function postCombatRollRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        combatId,
    }: PostCombatRollRequest): Promise<PostCombatRollResponse> {
        // An empty JSON object: FastEndpoints binds a request with no fields from it.
        const response = await axios.post<PostCombatRollResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats/${encodeURIComponent(combatId)}/roll`,
            {}
        );
        return response.data;
    };
}
