import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatResponseValidator } from "./combatResponse";

// Create Campaign
export type PostNextTurn = {
    combatId: string;
};

export type PostNextTurnResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function postNextTurnRequest(axios: AxiosInstance) {
    return async function (
        request: PostNextTurn,
    ): Promise<PostNextTurnResponse> {
        return await axios
            .post("/api/combat/next-turn", request)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
