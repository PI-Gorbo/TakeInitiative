import type { AxiosInstance } from "axios";
import { z } from "zod";
import { CombatState, plannedCombatValidator } from "../../types/models";
import { validateResponse } from "base/utils/apiErrorParser";

// Get User
export type GetCombatsRequest = {
    campaignId: string;
};

export const getCombatsResponseValidator = z.object({
    plannedCombats: z.array(plannedCombatValidator),
    combats: z.array(
        z
            .object({
                combatId: z.string(),
                combatName: z.string(),
                state: z.nativeEnum(CombatState),
                finishedTimestamp: z.string().nullable(),
            })
            .required({
                combatId: true,
                combatName: true,
            }),
    ),
});

export type GetCombatsResponse = z.infer<typeof getCombatsResponseValidator>;

export function getCombatsRequest(axios: AxiosInstance) {
    return function (request: GetCombatsRequest): Promise<GetCombatsResponse> {
        return axios
            .get("/api/combats", {
                params: {
                    campaignId: request.campaignId,
                },
            })
            .then((response) =>
                validateResponse(response, getCombatsResponseValidator),
            );
    };
}
