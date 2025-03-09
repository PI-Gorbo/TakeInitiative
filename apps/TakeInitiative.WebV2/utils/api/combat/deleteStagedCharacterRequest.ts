import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "~/utils/apiErrorParser";

export type DeleteStagedCharacterRequest = {
    combatId: string;
    characterId: string;
};

export type DeleteStagedCharacterResponse = z.infer<
    typeof combatResponseValidator
>;

export function deleteStagedCharacter(axios: AxiosInstance) {
    return async function (
        request: DeleteStagedCharacterRequest
    ): Promise<DeleteStagedCharacterResponse> {
        return await axios
            .delete("/api/combat/stage/character", { data: request })
            .then((response) =>
                validateResponse(response, combatResponseValidator)
            );
    };
}
