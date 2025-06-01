import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "~/utils/apiErrorParser";

export type PostStagePlayerCharactersRequest = {
    combatId: string;
    characterIds: string[];
};

export type PostStagePlayerCharactersResponse = z.infer<
    typeof combatResponseValidator
>;

export function postStagePlayerCharactersRequest(axios: AxiosInstance) {
    return async function (
        request: PostStagePlayerCharactersRequest
    ): Promise<PostStagePlayerCharactersResponse> {
        return await axios
            .post("/api/combat/stage/player-character", request)
            .then((response) =>
                validateResponse(response, combatResponseValidator)
            );
    };
}
