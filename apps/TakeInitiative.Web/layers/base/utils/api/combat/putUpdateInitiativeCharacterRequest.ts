import type { AxiosInstance } from "axios";
import { z } from "zod";
import {
    combatValidator,
    type CharacterHealth,
    type Condition,
} from "../../types/models";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

// Create Campaign
export type CombatCharacterDto = {
    id: string;
    name: string;
    health?: CharacterHealth;
    hidden: boolean;
    initiativeValue: number[];
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
