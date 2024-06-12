import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { CombatState, plannedCombatValidator } from "../../types/models";

// Get User
export type GetCombatsRequest = {
    campaignId: string;
};

export const getCombatsResponseValidator = yup.object({
    plannedCombats: yup.array(plannedCombatValidator),
    combats: yup.array(
        yup.object({
            combatId: yup.string().required(),
            combatName: yup.string().required(),
            state: yup.mixed().oneOf(Object.values(CombatState)),
            finishedTimestamp: yup.string().nullable(),
        }),
    ),
});

export type GetCombatsResponse = yup.InferType<
    typeof getCombatsResponseValidator
>;

export function getCombatsRequest(axios: AxiosInstance) {
    return function (request: GetCombatsRequest): Promise<GetCombatsResponse> {
        return axios
            .get("/api/combats", {
                params: {
                    campaignId: request.campaignId,
                },
            })
            .then(async function (response) {
                const result = await getCombatsResponseValidator.validate(
                    response.data,
                );
                return result;
            });
    };
}
