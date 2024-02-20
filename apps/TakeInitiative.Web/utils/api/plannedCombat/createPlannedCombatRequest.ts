import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { plannedCombatValidator } from "~/utils/types/models";

// Get User
export const createPlannedCombatRequestValidator = yup.object({
    campaignId: yup.string().required(),
    combatName: yup.string().required(),
});
export type CreatePlannedCombatRequest = yup.InferType<
    typeof createPlannedCombatRequestValidator
>;

export const postPlannedCombatResponseValidator = plannedCombatValidator;

export type CreatePlannedCombatResponse = yup.InferType<
    typeof postPlannedCombatResponseValidator
>;

export function createPlannedCombatRequest(axios: AxiosInstance) {
    return async function getUser(
        request: CreatePlannedCombatRequest,
    ): Promise<CreatePlannedCombatResponse> {
        return axios
            .post("/api/campaign/planned-combat", request)
            .then(async function (response) {
                const result =
                    await postPlannedCombatResponseValidator.validate(
                        response.data,
                    );
                return result;
            });
    };
}
