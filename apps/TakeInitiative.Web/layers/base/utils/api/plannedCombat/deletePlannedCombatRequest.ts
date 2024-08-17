import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { plannedCombatValidator } from "../../types/models";

// Get User
export const deletePlannedCombatRequestValidator = yup.object({
    campaignId: yup.string().required(),
    combatId: yup.string().required(),
});
export type DeletePlannedCombatRequest = yup.InferType<
    typeof deletePlannedCombatRequestValidator
>;

export function deletePlannedCombatRequest(axios: AxiosInstance) {
    return function getUser(
        request: DeletePlannedCombatRequest,
    ): Promise<void> {
        return axios.delete("/api/combat/planned", { data: request });
    };
}
