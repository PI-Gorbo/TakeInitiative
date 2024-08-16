import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatValidator } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";

// Create Campaign
export type GetCombatRequest = {
    combatId: string;
};

export type GetCombatResponse = yup.InferType<typeof combatResponseValidator>;

export function getCombatHistory(axios: AxiosInstance) {
    return async function (
        request: GetCombatRequest,
    ): Promise<GetCombatResponse> {
        return await axios
            .get(`/api/combat/${request.combatId}`)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
