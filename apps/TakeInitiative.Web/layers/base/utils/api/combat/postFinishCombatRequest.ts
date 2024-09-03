import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatValidator } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

// Create Campaign
export type PostFinishCombatRequest = {
    combatId: string;
};

export type PostFinishCombatResponse = z.infer<typeof combatResponseValidator>;

export function postFinishCombatRequest(axios: AxiosInstance) {
    return async function (
        request: PostFinishCombatRequest,
    ): Promise<PostFinishCombatResponse> {
        return await axios
            .post("/api/combat/finish", request)
            .then((response) =>
                validateResponse(response, combatResponseValidator),
            );
    };
}
