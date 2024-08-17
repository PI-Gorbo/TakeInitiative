import type { AxiosInstance } from "axios";
import { z } from "zod";
import {
    characterHealthValidator,
    characterInitiativeValidator,
    plannedCombatValidator,
} from "../../../../types/models";
import { validateResponse } from "base/utils/apiErrorParser";

// Get User
export const createPlannedCombatNpcRequestValidator = z
    .object({
        combatId: z.string(),
        stageId: z.string(),
        name: z.string(),
        health: characterHealthValidator,
        armourClass: z.number().nullable(),
        initiative: characterInitiativeValidator,
        quantity: z.number().min(1),
    })
    .required();

export type CreatePlannedCombatNpcRequest = z.infer<
    typeof createPlannedCombatNpcRequestValidator
>;

export const createPlannedCombatNpcResponseValidator = plannedCombatValidator;

export type CreatePlannedCombatStageResponse = z.infer<
    typeof createPlannedCombatNpcResponseValidator
>;

export function createPlannedCombatNpcRequest(axios: AxiosInstance) {
    return function getUser(
        request: CreatePlannedCombatNpcRequest,
    ): Promise<CreatePlannedCombatStageResponse> {
        return axios
            .post("/api/campaign/planned-combat/stage/npc", {
                ...request,
            } satisfies CreatePlannedCombatNpcRequest)
            .then((resp) =>
                validateResponse(resp, createPlannedCombatNpcResponseValidator),
            );
    };
}
