import type { AxiosInstance } from "axios";
import { z } from "zod";
import { plannedCombatValidator } from "../../../types/models";
import { validateResponse } from "base/utils/apiErrorParser";

export const updatePlannedCombatStageRequestValidator = z
    .object({
        combatId: z.string(),
        stageId: z.string(),
        name: z.string(),
    })
    .required();
export type UpdatePlannedCombatStageRequest = z.infer<
    typeof updatePlannedCombatStageRequestValidator
>;

export type UpdatePlannedCombatStageResponse = z.infer<
    typeof plannedCombatValidator
>;

export function updatePlannedCombatStageRequest(axios: AxiosInstance) {
    return async function getUser(
        request: UpdatePlannedCombatStageRequest,
    ): Promise<UpdatePlannedCombatStageResponse> {
        return axios
            .put("/api/campaign/planned-combat/stage", request)
            .then((resp) => validateResponse(resp, plannedCombatValidator));
    };
}
