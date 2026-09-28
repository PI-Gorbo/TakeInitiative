import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Delete Combatant (18a.6): a DM removes any, a player their own. The turn passes on.
export type DeleteCombatantRequest = ApiPathParams<"DeleteCombatant">;
export type DeleteCombatantResponse = ApiResponse<"DeleteCombatant">;
export function deleteCombatantRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        combatId,
        combatantId,
    }: DeleteCombatantRequest): Promise<DeleteCombatantResponse> {
        const response = await axios.delete<DeleteCombatantResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats/${encodeURIComponent(combatId)}/combatants/${encodeURIComponent(combatantId)}`
        );
        return response.data;
    };
}
