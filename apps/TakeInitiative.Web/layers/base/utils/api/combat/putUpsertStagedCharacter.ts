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
export type UpdateStagedCharacterRequest = {
    combatId: string;
    character: StagedCharacterDTO;
};

export type UpdateStagedCharacterResponse = z.infer<
    typeof combatResponseValidator
>;

export function putUpdateStagedCharacter(axios: AxiosInstance) {
    return async function (
        request: UpdateStagedCharacterRequest,
    ): Promise<UpdateStagedCharacterResponse> {
        return await axios
            .put("/api/combat/stage/character", request)
            .then(async (response) =>
                validateResponse(response, combatResponseValidator),
            );
    };
}
