import { characterHealthValidator, type CharacterHealth, type CharacterInitiative } from '../../types/models';
import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatValidator } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";

export type DeleteStagedCharacterRequest = {
    combatId: string,
    characterId: string
};

export type DeleteStagedCharacterResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function deleteStagedCharacter(axios: AxiosInstance) {
    return async function (
        request: DeleteStagedCharacterRequest,
    ): Promise<DeleteStagedCharacterResponse> {
        return await axios
            .delete("/api/combat/stage/character", {data: request})
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
