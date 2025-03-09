import type { AxiosInstance } from "axios";
import { z } from "zod";
import { plannedCombatValidator } from "../../../types/models";
import { validateResponse } from "~/utils/apiErrorParser";

// Get User
export const createPlannedCombatStageRequestValidator = z
    .object({
        combatId: z.string(),
        name: z.string(),
    })
    .required();
export type CreatePlannedCombatStageRequest = z.infer<
    typeof createPlannedCombatStageRequestValidator
>;

export const createPlannedCombatStageResponseValidator = plannedCombatValidator;

export type createPlannedCombatStageResponse = z.infer<
    typeof createPlannedCombatStageResponseValidator
>;

export function createPlannedCombatStageRequest(axios: AxiosInstance) {
    return async function getUser(
        request: CreatePlannedCombatStageRequest
    ): Promise<createPlannedCombatStageResponse> {
        return axios
            .post("/api/campaign/planned-combat/stage", request)
            .then((resp) =>
                validateResponse(
                    resp,
                    createPlannedCombatStageResponseValidator
                )
            );
    };
}
