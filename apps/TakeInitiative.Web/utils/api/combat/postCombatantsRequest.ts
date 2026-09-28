import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Combatants (18a.4): a DM adds any; a player adds one of their claimed characters, by
// `entryId` only, to an Active combat. They wait until the next roll.
export type PostCombatantsRequest = ApiPathParams<"PostCombatants"> &
    ApiRequestBody<"PostCombatants">;
export type PostCombatantsResponse = ApiResponse<"PostCombatants">;
export function postCombatantsRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        combatId,
        ...body
    }: PostCombatantsRequest): Promise<PostCombatantsResponse> {
        const response = await axios.post<PostCombatantsResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats/${encodeURIComponent(combatId)}/combatants`,
            body satisfies ApiRequestBody<"PostCombatants">
        );
        return response.data;
    };
}
