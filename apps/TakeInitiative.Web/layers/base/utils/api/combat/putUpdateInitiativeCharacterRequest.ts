import type { AxiosInstance } from "axios";
import { z } from "zod";
import {
    combatValidator,
    type UnevaluatedCharacterHealth,
    type Condition,
    type CharacterHealth,
    type CharacterInitiative,
} from "../../types/models";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

// Create Campaign
export type CombatCharacterDto = {
    id: string;
    name: string;
    health?: CharacterHealth;
    hidden: boolean;
    initiative: CharacterInitiative;
    armourClass: number | null;
    conditions: Condition[];
};
export type PutUpdateInitiativeCharacterRequest = {
    combatId: string;
    character: CombatCharacterDto;
};

export type PutUpdateInitiativeCharacterResponse = z.infer<
    typeof combatResponseValidator
>;

export function putUpdateInitiativeCharacterRequest(axios: AxiosInstance) {
    return async function (
        request: PutUpdateInitiativeCharacterRequest,
    ): Promise<PutUpdateInitiativeCharacterResponse> {
        return await axios
            .put("/api/combat/initiative/character", request)
            .then(async (response) =>
                validateResponse(response, combatResponseValidator),
            );
    };
}
