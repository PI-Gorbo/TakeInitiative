import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "~/utils/apiErrorParser";

// Create Campaign
export type PostStartCombatRequest = {
    combatId: string;
};

export type PostStartCombatResponse = z.infer<typeof combatResponseValidator>;

export function postStartCombatRequest(axios: AxiosInstance) {
    return async function (
        request: PostStartCombatRequest
    ): Promise<PostStartCombatResponse> {
        return await axios
            .post("/api/combat/roll-initiative", request)
            .then(async (response) =>
                validateResponse(response, combatResponseValidator)
            );
    };
}
