import type { AxiosInstance } from "axios";
import { z } from "zod";
import { draftCombatValidator } from "../../types/models";
import { validateResponse } from "~/utils/apiErrorParser";

// Get User
export const updatePlannedCombatRequestValidator = z.object({
    plannedCombatId: z.string(),
    combatName: z.string(),
});
export type UpdatePlannedCombatRequest = z.infer<
    typeof updatePlannedCombatRequestValidator
>;

export const updatePlannedCombatResponseValidator = draftCombatValidator;

export type UpdatePlannedCombatResponse = z.infer<
    typeof updatePlannedCombatResponseValidator
>;

export function updatePlannedCombatRequest(axios: AxiosInstance) {
    return async function getUser(
        request: UpdatePlannedCombatRequest
    ): Promise<UpdatePlannedCombatResponse> {
        return axios
            .post(`/api/combat/planned/${request.plannedCombatId}`, { combatName: request.combatName })
            .then((resp) =>
                validateResponse(resp, updatePlannedCombatResponseValidator)
            );
    };
}
