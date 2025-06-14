import type { AxiosInstance } from "axios";
import { z } from "zod";
import {
    unevaluatedCharacterHealthValidator,
    unevaluatedCharacterInitiativeValidator,
    draftCombatValidator,
} from "../../../../types/models";
import { validateResponse } from "~/utils/apiErrorParser";

// Get User
export const deletePlannedCombatNpcRequestValidator = z
    .object({
        combatId: z.string(),
        stageId: z.string(),
        npcId: z.string(),
    })
    .required();

export type DeletePlannedCombatNpcRequest = z.infer<
    typeof deletePlannedCombatNpcRequestValidator
>;

export type deletePlannedCombatStageResponse = z.infer<
    typeof draftCombatValidator
>;

export function deletePlannedCombatNpcRequest(axios: AxiosInstance) {
    return async function getUser(
        request: DeletePlannedCombatNpcRequest
    ): Promise<deletePlannedCombatStageResponse> {
        return axios
            .delete("/api/campaign/planned-combat/stage/npc", { data: request })
            .then((resp) => validateResponse(resp, draftCombatValidator));
    };
}
