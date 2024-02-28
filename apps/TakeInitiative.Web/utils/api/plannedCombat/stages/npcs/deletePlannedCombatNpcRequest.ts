import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { characterHealthValidator, characterInitiativeValidator, plannedCombatValidator } from "~/utils/types/models";

// Get User
export const deletePlannedCombatNpcRequestValidator = yup.object({
    combatId: yup.string().required(),
	stageId: yup.string().required(),
	npcId: yup.string().required(),
});

export type DeletePlannedCombatNpcRequest = yup.InferType<
    typeof deletePlannedCombatNpcRequestValidator
>;

export const deletePlannedCombatNpcResponseValidator = plannedCombatValidator;

export type deletePlannedCombatStageResponse = yup.InferType<
    typeof deletePlannedCombatNpcResponseValidator
>;

export function deletePlannedCombatNpcRequest(axios: AxiosInstance) {
    return async function getUser(
        request: DeletePlannedCombatNpcRequest,
    ): Promise<deletePlannedCombatStageResponse> {
        return axios
            .delete("/api/campaign/planned-combat/stage/npc", {data: request})
            .then(async function (response) {
                const result =
                    await deletePlannedCombatNpcResponseValidator.validate(
                        response.data,
                    );
                return result;
            });
    };
}
