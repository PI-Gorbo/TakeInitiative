import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatValidator } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";

// Create Campaign
export type PostStartCombatRequest = {
    combatId: string;
};

export type PostStartCombatResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function postStartCombatRequest(axios: AxiosInstance) {
    return async function (
        request: PostStartCombatRequest,
    ): Promise<PostStartCombatResponse> {
        return await axios
            .post("/api/combat/start", request)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
