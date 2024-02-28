import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { characterHealthValidator, characterInitiativeValidator, plannedCombatValidator } from "~/utils/types/models";

// Get User
export const createPlannedCombatNpcRequestValidator = yup.object({
    combatId: yup.string().required(),
	stageId: yup.string().required(),
	name: yup.string().required(),
	health: characterHealthValidator.nullable(),
	armourClass: yup.number().nullable(),
	initiative: characterInitiativeValidator.required(),
	quantity: yup.number().required().min(1)	
});

export type CreatePlannedCombatNpcRequest = yup.InferType<
    typeof createPlannedCombatNpcRequestValidator
>;

export const createPlannedCombatNpcResponseValidator = plannedCombatValidator;

export type createPlannedCombatStageResponse = yup.InferType<
    typeof createPlannedCombatNpcResponseValidator
>;

export function createPlannedCombatNpcRequest(axios: AxiosInstance) {
    return async function getUser(
        request: CreatePlannedCombatNpcRequest,
    ): Promise<createPlannedCombatStageResponse> {
        return axios
            .post("/api/campaign/planned-combat/stage/npc", request)
            .then(async function (response) {
                const result =
                    await createPlannedCombatNpcResponseValidator.validate(
                        response.data,
                    );
                return result;
            });
    };
}
