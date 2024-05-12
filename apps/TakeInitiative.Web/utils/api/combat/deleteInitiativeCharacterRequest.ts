import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatResponseValidator } from "./combatResponse";

// Create Campaign
export type DeleteInitiativeCharacterRequest = {
    combatId: string;
    characterId: string;
};

export type DeleteInitiativeCharacterResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function deleteInitiativeCharacterRequest(axios: AxiosInstance) {
    return async function (
        request: DeleteInitiativeCharacterRequest,
    ): Promise<DeleteInitiativeCharacterResponse> {
        return await axios
            .delete("/api/combat/initiative/character", { data: request })
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
