import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatValidator } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

// Create Campaign
export type OpenCombatRequest = {
    plannedCombatId: string;
};

export type OpenCombatResponse = z.infer<typeof combatResponseValidator>;

export function openCombatRequest(axios: AxiosInstance) {
    return async function (
        request: OpenCombatRequest,
    ): Promise<OpenCombatResponse> {
        return await axios
            .post("/api/combat/start", request)
            .then(async (response) =>
                validateResponse(response, combatResponseValidator),
            );
    };
}
