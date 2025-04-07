import type { AxiosInstance } from "axios";
import { z } from "zod";
import { draftCombatValidator } from "../../../types/models";
import { validateResponse } from "~/utils/apiErrorParser";

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
    typeof draftCombatValidator
>;

export function updatePlannedCombatStageRequest(axios: AxiosInstance) {
    return async function getUser(
        request: UpdatePlannedCombatStageRequest
    ): Promise<UpdatePlannedCombatStageResponse> {
        return axios
            .put("/api/campaign/planned-combat/stage", request)
            .then((resp) => validateResponse(resp, draftCombatValidator));
    };
}
