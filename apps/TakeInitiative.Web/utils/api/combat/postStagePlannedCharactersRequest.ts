import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatResponseValidator } from "./combatResponse";

// Create Campaign
export type StagePlannedCharacterDto = {
    characterId: string;
    quantity: number;
};
export type PostStagePlannedCharactersRequest = {
    combatId: string;
    plannedCharactersToStage: Record<string, StagePlannedCharacterDto[]>;
};

export type PostStagePlannedCharactersResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function postStagedPlannedCharactersRequest(axios: AxiosInstance) {
    return async function (
        request: PostStagePlannedCharactersRequest,
    ): Promise<PostStagePlannedCharactersResponse> {
        return await axios
            .post("/api/combat/stage/planned-character", request)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
