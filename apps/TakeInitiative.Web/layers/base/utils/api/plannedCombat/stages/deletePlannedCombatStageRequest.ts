import type { AxiosInstance } from "axios";
import { z } from "zod";
import { plannedCombatValidator } from "../../../types/models";
import { validateResponse } from "base/utils/apiErrorParser";

export const deletePlannedCombatStageRequestValidator = z
    .object({
        combatId: z.string(),
        stageId: z.string(),
    })
    .required();
export type DeletePlannedCombatStageRequest = z.infer<
    typeof deletePlannedCombatStageRequestValidator
>;

export const deletePlannedCombatStageResponseValidator = plannedCombatValidator;

export type DeletePlannedCombatStageResponse = z.infer<
    typeof deletePlannedCombatStageResponseValidator
>;

export function deletePlannedCombatStageRequest(axios: AxiosInstance) {
    return async function getUser(
        request: DeletePlannedCombatStageRequest,
    ): Promise<DeletePlannedCombatStageResponse> {
        return axios
            .delete("/api/campaign/planned-combat/stage", { data: request })
            .then((resp) =>
                validateResponse(
                    resp,
                    deletePlannedCombatStageResponseValidator,
                ),
            );
    };
}
