import type { AxiosInstance } from "axios";
import { z } from "zod";
import {
    unevaluatedCharacterHealthValidator,
    unevaluatedCharacterInitiativeValidator,
    draftCombatValidator,
} from "../../../../types/models";
import { validateResponse } from "~/utils/apiErrorParser";

// Get User
export const createPlannedCombatNpcRequestValidator = z
    .object({
        combatId: z.string(),
        stageId: z.string(),
        name: z.string(),
        health: unevaluatedCharacterHealthValidator,
        armourClass: z.number().nullable(),
        initiative: unevaluatedCharacterInitiativeValidator,
        quantity: z.number().min(1),
    })
    .required();

export type CreatePlannedCombatNpcRequest = z.infer<
    typeof createPlannedCombatNpcRequestValidator
>;

export const createPlannedCombatNpcResponseValidator = draftCombatValidator;

export type CreatePlannedCombatStageResponse = z.infer<
    typeof createPlannedCombatNpcResponseValidator
>;

export function createPlannedCombatNpcRequest(axios: AxiosInstance) {
    return function getUser(
        request: CreatePlannedCombatNpcRequest
    ): Promise<CreatePlannedCombatStageResponse> {
        return axios
            .post("/api/campaign/planned-combat/stage/npc", {
                ...request,
            } satisfies CreatePlannedCombatNpcRequest)
            .then((resp) =>
                validateResponse(resp, createPlannedCombatNpcResponseValidator)
            );
    };
}
