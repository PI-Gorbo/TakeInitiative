import type { AxiosInstance } from "axios";
import { z } from "zod";
import { draftCombatValidator } from "../../types/models";
import { validateResponse } from "~/utils/apiErrorParser";

// Get User
export const createPlannedCombatRequestValidator = z.object({
    campaignId: z.string().uuid(),
    combatId: z.string().uuid(),
});
export type GetPlannedCombatRequest = z.infer<
    typeof createPlannedCombatRequestValidator
>;

export type GetPlannedCombatResponse = z.infer<
    typeof draftCombatValidator
>;

export function getPlannedCombatRequest(axios: AxiosInstance) {
    return async function (
        request: GetPlannedCombatRequest
    ): Promise<GetPlannedCombatResponse> {
        return axios
            .get("/api/combat/planned", { params: request })
            .then((resp) =>
                validateResponse(resp, draftCombatValidator)
            );
    };
}
