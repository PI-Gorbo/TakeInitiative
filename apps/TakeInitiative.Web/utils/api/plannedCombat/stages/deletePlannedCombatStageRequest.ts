import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { plannedCombatValidator } from "base/utils/types/models";

export const deletePlannedCombatStageRequestValidator = yup.object({
    combatId: yup.string().required(),
    stageId: yup.string().required(),
});
export type DeletePlannedCombatStageRequest = yup.InferType<
    typeof deletePlannedCombatStageRequestValidator
>;

export const deletePlannedCombatStageResponseValidator = plannedCombatValidator;

export type DeletePlannedCombatStageResponse = yup.InferType<
    typeof deletePlannedCombatStageResponseValidator
>;

export function deletePlannedCombatStageRequest(axios: AxiosInstance) {
    return async function getUser(
        request: DeletePlannedCombatStageRequest,
    ): Promise<DeletePlannedCombatStageResponse> {
        return axios
            .delete("/api/campaign/planned-combat/stage", { data: request })
            .then(async function (response) {
                const result =
                    await deletePlannedCombatStageResponseValidator.validate(
                        response.data,
                    );
                return result;
            });
    };
}
