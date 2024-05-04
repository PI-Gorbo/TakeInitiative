import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatResponseValidator } from "./combatResponse";

// Create Campaign
export type PostRollStagedCharactersIntoInitiativeRequest = {
    combatId: string;
    characterIds: string[];
};

export type PostRollStagedCharactersIntoInitiativeResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function postRollStagedCharactersIntoInitiativeRequest(
    axios: AxiosInstance,
) {
    return async function (
        request: PostRollStagedCharactersIntoInitiativeRequest,
    ): Promise<PostRollStagedCharactersIntoInitiativeResponse> {
        return await axios
            .post("/api/combat/stage/roll", request)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
