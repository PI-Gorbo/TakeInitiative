import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Combat (members): the combat redacted for the caller (18a.5). A Draft, for a
// player, is a 404.
export type GetCombatRequest = ApiPathParams<"GetCombat">;
export type GetCombatResponse = ApiResponse<"GetCombat">;
export function getCombatRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        combatId,
    }: GetCombatRequest): Promise<GetCombatResponse> {
        const response = await axios.get<GetCombatResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats/${encodeURIComponent(combatId)}`
        );
        return response.data;
    };
}
