import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { plannedCombatValidator } from "base/utils/types/models";

// Get User
export const createPlannedCombatStageRequestValidator = yup.object({
    combatId: yup.string().required(),
    name: yup.string().required(),
});
export type CreatePlannedCombatStageRequest = yup.InferType<
    typeof createPlannedCombatStageRequestValidator
>;

export const createPlannedCombatStageResponseValidator = plannedCombatValidator;

export type createPlannedCombatStageResponse = yup.InferType<
    typeof createPlannedCombatStageResponseValidator
>;

export function createPlannedCombatStageRequest(axios: AxiosInstance) {
    return async function getUser(
        request: CreatePlannedCombatStageRequest,
    ): Promise<createPlannedCombatStageResponse> {
        return axios
            .post("/api/campaign/planned-combat/stage", request)
            .then(async function (response) {
                const result =
                    await createPlannedCombatStageResponseValidator.validate(
                        response.data,
                    );
                return result;
            });
    };
}
