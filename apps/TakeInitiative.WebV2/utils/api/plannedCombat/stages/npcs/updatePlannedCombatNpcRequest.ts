import type { AxiosInstance } from "axios";
import { z } from "zod";
import {
    unevaluatedCharacterHealthValidator,
    unevaluatedCharacterInitiativeValidator,
    plannedCombatValidator,
} from "../../../../types/models";
import { validateResponse } from "~/utils/apiErrorParser";

// Get User
export const updatePlannedCombatNpcRequestValidator = z
    .object({
        combatId: z.string(),
        stageId: z.string(),
        npcId: z.string(),
        name: z.string(),
        health: unevaluatedCharacterHealthValidator,
        armourClass: z.number().nullable(),
        initiative: unevaluatedCharacterInitiativeValidator,
        quantity: z.number().min(1),
    })
    .required({
        combatId: true,
        stageId: true,
        npcId: true,
        initiative: true,
        quantity: true,
    });

export type UpdatePlannedCombatNpcRequest = z.infer<
    typeof updatePlannedCombatNpcRequestValidator
>;

export const updatePlannedCombatNpcResponseValidator = plannedCombatValidator;

export type updatePlannedCombatStageResponse = z.infer<
    typeof updatePlannedCombatNpcResponseValidator
>;

export function updatePlannedCombatNpcRequest(axios: AxiosInstance) {
    return async function getUser(
        request: UpdatePlannedCombatNpcRequest
    ): Promise<updatePlannedCombatStageResponse> {
        return axios
            .put("/api/campaign/planned-combat/stage/npc", request)
            .then((resp) =>
                validateResponse(resp, updatePlannedCombatNpcResponseValidator)
            );
    };
}
