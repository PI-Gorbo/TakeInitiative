import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Combatant (18a.6): the combatant's whole editable state. A player may change only their own
// HP, max HP, conditions and, while waiting, initiative.
export type PutCombatantRequest = ApiPathParams<"PutCombatant"> &
    ApiRequestBody<"PutCombatant">;
export type PutCombatantResponse = ApiResponse<"PutCombatant">;
export function putCombatantRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        combatId,
        combatantId,
        ...body
    }: PutCombatantRequest): Promise<PutCombatantResponse> {
        const response = await axios.put<PutCombatantResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats/${encodeURIComponent(combatId)}/combatants/${encodeURIComponent(combatantId)}`,
            body satisfies ApiRequestBody<"PutCombatant">
        );
        return response.data;
    };
}
