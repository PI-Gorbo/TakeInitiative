import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Combat End Turn (18b.2): the body names the turn being ended. A stale one is a 409 "The turn
// has already moved on.", which the web treats as done.
export type PostCombatEndTurnRequest = ApiPathParams<"PostCombatEndTurn"> &
    ApiRequestBody<"PostCombatEndTurn">;
export type PostCombatEndTurnResponse = ApiResponse<"PostCombatEndTurn">;
export function postCombatEndTurnRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        combatId,
        ...body
    }: PostCombatEndTurnRequest): Promise<PostCombatEndTurnResponse> {
        const response = await axios.post<PostCombatEndTurnResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats/${encodeURIComponent(combatId)}/end-turn`,
            body satisfies ApiRequestBody<"PostCombatEndTurn">
        );
        return response.data;
    };
}
