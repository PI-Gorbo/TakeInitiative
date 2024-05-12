import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatValidator, type CharacterHealth } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";

// Create Campaign
export type CombatCharacterDto = {
    id: string;
    name: string;
    health?: CharacterHealth;
    hidden: boolean;
    initiativeValue: number[];
    armorClass: number | null;
};
export type PutUpdateInitiativeCharacterRequest = {
    combatId: string;
    character: CombatCharacterDto;
};

export type PutUpdateInitiativeCharacterResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function putUpdateInitiativeCharacterRequest(axios: AxiosInstance) {
    return async function (
        request: PutUpdateInitiativeCharacterRequest,
    ): Promise<PutUpdateInitiativeCharacterResponse> {
        return await axios
            .put("/api/combat/initiative/character", request)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
