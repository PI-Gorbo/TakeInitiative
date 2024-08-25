import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatValidator } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

// Create Campaign
export type GetCombatRequest = {
    combatId: string;
};

export type GetCombatResponse = z.infer<typeof combatResponseValidator>;

export function getCombatRequest(axios: AxiosInstance) {
    return async function (
        request: GetCombatRequest,
    ): Promise<GetCombatResponse> {
        return await axios
            .get(`/api/combat/${request.combatId}`)
            .then((response) =>
                validateResponse(response, combatResponseValidator),
            );
    };
}
