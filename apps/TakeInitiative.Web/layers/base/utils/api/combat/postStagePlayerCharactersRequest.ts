import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

export type PostStagePlannedCharactersRequest = {
    combatId: string;
    characterIds: string[];
};

export type PostStagePlannedCharactersResponse = z.infer<
    typeof combatResponseValidator
>;

export function postStagePlayerCharactersRequest(axios: AxiosInstance) {
    return async function (
        request: PostStagePlannedCharactersRequest,
    ): Promise<PostStagePlannedCharactersResponse> {
        return await axios
            .post("/api/combat/stage/player-character", request)
            .then((response) =>
                validateResponse(response, combatResponseValidator),
            );
    };
}
