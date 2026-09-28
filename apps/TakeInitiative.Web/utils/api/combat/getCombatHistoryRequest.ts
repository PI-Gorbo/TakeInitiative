import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Combat History (DMs): one row per event, oldest first (18b.6).
export type GetCombatHistoryRequest = ApiPathParams<"GetCombatHistory">;
export type GetCombatHistoryResponse = ApiResponse<"GetCombatHistory">;
export function getCombatHistoryRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        combatId,
    }: GetCombatHistoryRequest): Promise<GetCombatHistoryResponse> {
        const response = await axios.get<GetCombatHistoryResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats/${encodeURIComponent(combatId)}/history`
        );
        return response.data;
    };
}
