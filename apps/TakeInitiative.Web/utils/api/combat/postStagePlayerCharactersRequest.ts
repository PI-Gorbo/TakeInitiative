import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatResponseValidator } from "./combatResponse";

export type PostStagePlannedCharactersRequest = {
    combatId: string;
    characterIds: string[];
};

export type PostStagePlannedCharactersResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function postStagePlayerCharactersRequest(axios: AxiosInstance) {
    return async function (
        request: PostStagePlannedCharactersRequest,
    ): Promise<PostStagePlannedCharactersResponse> {
        return await axios
            .post("/api/combat/stage/player-character", request)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
