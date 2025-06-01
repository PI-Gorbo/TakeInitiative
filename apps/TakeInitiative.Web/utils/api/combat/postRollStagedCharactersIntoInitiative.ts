import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "~/utils/apiErrorParser";

// Create Campaign
export type PostRollStagedCharactersIntoInitiativeRequest = {
    combatId: string;
    characterIds: string[];
};

export type PostRollStagedCharactersIntoInitiativeResponse = z.infer<
    typeof combatResponseValidator
>;

export function postRollStagedCharactersIntoInitiativeRequest(
    axios: AxiosInstance
) {
    return async function (
        request: PostRollStagedCharactersIntoInitiativeRequest
    ): Promise<PostRollStagedCharactersIntoInitiativeResponse> {
        return await axios
            .post("/api/combat/stage/roll", request)
            .then(async (response) =>
                validateResponse(response, combatResponseValidator)
            );
    };
}
