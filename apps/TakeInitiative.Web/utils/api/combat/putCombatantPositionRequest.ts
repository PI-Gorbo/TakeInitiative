import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Combatant Position (DMs, 18b.4): moves a placed combatant to just after `afterId`, or to the
// top.
export type PutCombatantPositionRequest =
    ApiPathParams<"PutCombatantPosition"> &
        ApiRequestBody<"PutCombatantPosition">;
export type PutCombatantPositionResponse = ApiResponse<"PutCombatantPosition">;
export function putCombatantPositionRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        combatId,
        combatantId,
        ...body
    }: PutCombatantPositionRequest): Promise<PutCombatantPositionResponse> {
        const response = await axios.put<PutCombatantPositionResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats/${encodeURIComponent(combatId)}/combatants/${encodeURIComponent(combatantId)}/position`,
            body satisfies ApiRequestBody<"PutCombatantPosition">
        );
        return response.data;
    };
}
