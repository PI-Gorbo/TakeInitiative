import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatValidator } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";

// Create Campaign
export type OpenCombatRequest = {
    plannedCombatId: string;
};

export type OpenCombatResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function openCombatRequest(axios: AxiosInstance) {
    return async function (
        request: OpenCombatRequest,
    ): Promise<OpenCombatResponse> {
        return await axios
            .post("/api/combat/open", request)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
