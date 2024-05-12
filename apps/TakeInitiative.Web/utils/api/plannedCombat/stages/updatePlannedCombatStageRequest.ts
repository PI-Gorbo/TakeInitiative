import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { plannedCombatValidator } from "~/utils/types/models";

export const updatePlannedCombatStageRequestValidator = yup.object({
    combatId: yup.string().required(),
    stageId: yup.string().required(),
    name: yup.string().required(),
});
export type UpdatePlannedCombatStageRequest = yup.InferType<
    typeof updatePlannedCombatStageRequestValidator
>;

export const updatePlannedCombatStageResponseValidator = plannedCombatValidator;
export type UpdatePlannedCombatStageResponse = yup.InferType<
    typeof updatePlannedCombatStageResponseValidator
>;

export function updatePlannedCombatStageRequest(axios: AxiosInstance) {
    return async function getUser(
        request: UpdatePlannedCombatStageRequest,
    ): Promise<UpdatePlannedCombatStageResponse> {
        return axios
            .put("/api/campaign/planned-combat/stage", request)
            .then(async function (response) {
                const result =
                    await updatePlannedCombatStageResponseValidator.validate(
                        response.data,
                    );
                return result;
            });
    };
}
