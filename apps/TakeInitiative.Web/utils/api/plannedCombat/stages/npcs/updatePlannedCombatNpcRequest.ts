import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { characterHealthValidator, characterInitiativeValidator, plannedCombatValidator } from "~/utils/types/models";

// Get User
export const updatePlannedCombatNpcRequestValidator = yup.object({
    combatId: yup.string().required(),
	stageId: yup.string().required(),
	npcId: yup.string().required(),
	name: yup.string().required(),
	health: characterHealthValidator.nullable(),
	armourClass: yup.number().nullable(),
	initiative: characterInitiativeValidator.required(),
	quantity: yup.number().required().min(1)	
});

export type UpdatePlannedCombatNpcRequest = yup.InferType<
    typeof updatePlannedCombatNpcRequestValidator
>;

export const updatePlannedCombatNpcResponseValidator = plannedCombatValidator;

export type updatePlannedCombatStageResponse = yup.InferType<
    typeof updatePlannedCombatNpcResponseValidator
>;

export function updatePlannedCombatNpcRequest(axios: AxiosInstance) {
    return async function getUser(
        request: UpdatePlannedCombatNpcRequest,
    ): Promise<updatePlannedCombatStageResponse> {
        return axios
            .put("/api/campaign/planned-combat/stage/npc", request)
            .then(async function (response) {
                const result =
                    await updatePlannedCombatNpcResponseValidator.validate(
                        response.data,
                    );
                return result;
            });
    };
}
