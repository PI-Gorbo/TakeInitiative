import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

// Create Campaign
export type DeleteInitiativeCharacterRequest = {
    combatId: string;
    characterId: string;
};

export type DeleteInitiativeCharacterResponse = z.infer<
    typeof combatResponseValidator
>;

export function deleteInitiativeCharacterRequest(axios: AxiosInstance) {
    return async function (
        request: DeleteInitiativeCharacterRequest,
    ): Promise<DeleteInitiativeCharacterResponse> {
        return await axios
            .delete("/api/combat/initiative/character", { data: request })
            .then((response) =>
                validateResponse(response, combatResponseValidator),
            );
    };
}
