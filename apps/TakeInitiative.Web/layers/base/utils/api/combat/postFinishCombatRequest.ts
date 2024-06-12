import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatValidator } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";

// Create Campaign
export type PostFinishCombatRequest = {
    combatId: string;
};

export type PostFinishCombatResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function postFinishCombatRequest(axios: AxiosInstance) {
    return async function (
        request: PostFinishCombatRequest,
    ): Promise<PostFinishCombatResponse> {
        return await axios
            .post("/api/combat/finish", request)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
