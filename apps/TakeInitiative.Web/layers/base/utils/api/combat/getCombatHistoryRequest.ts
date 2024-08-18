import type { AxiosInstance } from "axios";
import { z } from "zod";
import { combatValidator, historyEntryValidator } from "../../types/models";
import { combatResponseValidator } from "./combatResponse";
import { validateResponse } from "base/utils/apiErrorParser";

// Create Campaign
export type GetCombatRequest = {
    combatId: string;
};

const getCombatHistoryResponseValidator = z.object({
    history: z.array(historyEntryValidator),
});
export type GetCombatHistoryResponse = z.infer<
    typeof getCombatHistoryResponseValidator
>;

export function getCombatHistory(axios: AxiosInstance) {
    return async function (
        request: GetCombatRequest,
    ): Promise<GetCombatHistoryResponse> {
        return await axios
            .get(`/api/combat/${request.combatId}/history`)
            .then((response) =>
                validateResponse(response, getCombatHistoryResponseValidator),
            );
    };
}
