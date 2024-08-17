import {
    type CharacterHealth,
    type CharacterInitiative,
} from "../../types/models";
import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

export type StagedCharacterDTO = {
    id: string;
    name: string;
    health: CharacterHealth | null;
    initiative: CharacterInitiative;
    armourClass: number | null;
    hidden: boolean;
};
export type UpsertStagedCharacterRequest = {
    combatId: string;
    character: StagedCharacterDTO;
};

export type UpsertStagedCharacterResponse = z.infer<
    typeof combatResponseValidator
>;

export function putUpsertStagedCharacter(axios: AxiosInstance) {
    return async function (
        request: UpsertStagedCharacterRequest,
    ): Promise<UpsertStagedCharacterResponse> {
        return await axios
            .put("/api/combat/stage/character", request)
            .then(async (response) =>
                validateResponse(response, combatResponseValidator),
            );
    };
}
