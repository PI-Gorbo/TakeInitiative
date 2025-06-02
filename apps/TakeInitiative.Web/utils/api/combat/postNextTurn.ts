import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "~/utils/apiErrorParser";

// Create Campaign
export type PostEndTurn = {
    combatId: string;
};

export type PostEndTurnResponse = z.infer<typeof combatResponseValidator>;

export function postEndTurnRequest(axios: AxiosInstance) {
    return async function (request: PostEndTurn): Promise<PostEndTurnResponse> {
        return await axios
            .post("/api/combat/turn/end", request)
            .then(async (response) =>
                validateResponse(response, combatResponseValidator)
            );
    };
}
