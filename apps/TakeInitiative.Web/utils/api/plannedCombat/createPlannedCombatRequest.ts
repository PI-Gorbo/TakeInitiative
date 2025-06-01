import type { AxiosInstance } from "axios";
import { z } from "zod";
import { draftCombatValidator } from "../../types/models";
import { validateResponse } from "~/utils/apiErrorParser";

// Get User
export const createPlannedCombatRequestValidator = z.object({
    campaignId: z.string(),
    combatName: z.string(),
});
export type CreatePlannedCombatRequest = z.infer<
    typeof createPlannedCombatRequestValidator
>;

export const postPlannedCombatResponseValidator = draftCombatValidator;

export type CreatePlannedCombatResponse = z.infer<
    typeof postPlannedCombatResponseValidator
>;

export function createPlannedCombatRequest(axios: AxiosInstance) {
    return async function getUser(
        request: CreatePlannedCombatRequest
    ): Promise<CreatePlannedCombatResponse> {
        return axios
            .post("/api/combat/planned", request)
            .then((resp) =>
                validateResponse(resp, postPlannedCombatResponseValidator)
            );
    };
}
