import { characterHealthValidator, type CharacterHealth, type CharacterInitiative } from './../../types/models';
import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatValidator } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";

export type StagedCharacterDTO = {
    id: string,
    name: string,
    health: CharacterHealth | null, 
    initiative: CharacterInitiative
    armorClass: number | null
    hidden: boolean
}
export type UpsertStagedCharacterRequest = {
    combatId: string,
    character: StagedCharacterDTO
};

export type UpsertStagedCharacterResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function putUpsertStagedCharacter(axios: AxiosInstance) {
    return async function (
        request: UpsertStagedCharacterRequest,
    ): Promise<UpsertStagedCharacterResponse> {
        return await axios
            .put("/api/combat/staged-character", request)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
