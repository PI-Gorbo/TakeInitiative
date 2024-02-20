import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { plannedCombatValidator } from "~/utils/types/models";

// Get User
export type GetPlannedCombatsRequest = {
    campaignId: string;
};

export const getPlannedCombatResponseValidator = yup.object({
    plannedCombats: yup.array(plannedCombatValidator),
});

export type PostPlannedCombatResponse = yup.InferType<
    typeof getPlannedCombatResponseValidator
>;

export function getPlannedCombatsRequest(axios: AxiosInstance) {
    return async function getUser(
        request: GetPlannedCombatsRequest,
    ): Promise<PostPlannedCombatResponse> {
        return axios
            .get("/api/campaign/planned-combats", { data: request })
            .then(async function (response) {
                const result = await getPlannedCombatResponseValidator.validate(
                    response.data,
                );
                return result;
            });
    };
}
