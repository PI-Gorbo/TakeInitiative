import type { AxiosInstance } from "axios";
import { z } from "zod";

// Get User
export const deletePlannedCombatRequestValidator = z.object({
    campaignId: z.string(),
    combatId: z.string(),
});
export type DeletePlannedCombatRequest = z.infer<
    typeof deletePlannedCombatRequestValidator
>;

export function deletePlannedCombatRequest(axios: AxiosInstance) {
    return function getUser(
        request: DeletePlannedCombatRequest,
    ): Promise<void> {
        return axios.delete("/api/combat/planned", { data: request });
    };
}
