import type { AxiosInstance } from "axios";
import { z } from "zod";
import { CombatState, draftCombatValidator } from "../../types/models";
import { validateResponse } from "~/utils/apiErrorParser";

// Get User
export type GetCombatsRequest = {
    campaignId: string;
};
const combatHistoryDto = z.array(
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
        })
)
export type CombatHistoryDto = z.infer<typeof combatHistoryDto>;

export const getCombatsResponseValidator = z.object({
    plannedCombats: z.array(z.object({
        id: z.string().uuid(),
        name: z.string()
    })),
    combats: combatHistoryDto,
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
                validateResponse(response, getCombatsResponseValidator)
            );
    };
}
