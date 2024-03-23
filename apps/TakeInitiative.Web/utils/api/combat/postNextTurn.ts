import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { combatResponseValidator } from "./combatResponse";

// Create Campaign
export type PostEndTurn = {
    combatId: string;
};

export type PostEndTurnResponse = yup.InferType<
    typeof combatResponseValidator
>;

export function postEndTurnRequest(axios: AxiosInstance) {
    return async function (
        request: PostEndTurn,
    ): Promise<PostEndTurnResponse> {
        return await axios
            .post("/api/combat/turn/end", request)
            .then(async (response) =>
                validateWithSchema(response.data, combatResponseValidator),
            );
    };
}
