import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

// Create Campaign
export type StagePlannedCharacterDto = {
    characterId: string;
    quantity: number;
};
export type PostStagePlannedCharactersRequest = {
    combatId: string;
    plannedCharactersToStage: Record<string, StagePlannedCharacterDto[]>;
};

export type PostStagePlannedCharactersResponse = z.infer<
    typeof combatResponseValidator
>;

export function postStagedPlannedCharactersRequest(axios: AxiosInstance) {
    return async function (
        request: PostStagePlannedCharactersRequest,
    ): Promise<PostStagePlannedCharactersResponse> {
        return await axios
            .post("/api/combat/stage/planned-character", request)
            .then(async (response) =>
                validateResponse(response, combatResponseValidator),
            );
    };
}
