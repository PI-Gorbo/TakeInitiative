import {
    type UnevaluatedCharacterHealth,
    type UnevaluatedCharacterInitiative,
} from "../../types/models";
import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

export type StagedCharacterWithoutIdDTO = {
    name: string;
    health: UnevaluatedCharacterHealth | null;
    initiative: UnevaluatedCharacterInitiative;
    armourClass: number | null;
    hidden: boolean;
};
export type AddStagedCharacterRequest = {
    combatId: string;
    character: StagedCharacterWithoutIdDTO;
};

export type AddStagedCharacterResponse = z.infer<
    typeof combatResponseValidator
>;

export function postAddStagedCharacter(axios: AxiosInstance) {
    return async function (
        request: AddStagedCharacterRequest,
    ): Promise<AddStagedCharacterResponse> {
        return await axios
            .post("/api/combat/stage/character", request)
            .then(async (response) =>
                validateResponse(response, combatResponseValidator),
            );
    };
}
