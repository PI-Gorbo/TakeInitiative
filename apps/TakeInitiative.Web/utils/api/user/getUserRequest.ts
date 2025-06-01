import type { AxiosInstance } from "axios";
import { z } from "zod";

// Get User
const getUserCampaignDto = z
    .object({
        campaignName: z.string(),
        campaignId: z.string(),
        joinCode: z.string(),
        currentCombatName: z.string().nullable()
    })
    .required();

export const getUserResponseSchema = z
    .object({
        userId: z.string(),
        username: z.string(),
        confirmedEmail: z.boolean(),
        dmCampaigns: z.array(getUserCampaignDto),
        memberCampaigns: z.array(getUserCampaignDto),
    })
    .required();
export type GetUserResponse = z.infer<typeof getUserResponseSchema>;
export function getUserRequest(axios: AxiosInstance) {
    return async function getUser(): Promise<GetUserResponse> {
        return axios
            .get("/api/user", { data: {} })
            .then((resp) => validateResponse(resp, getUserResponseSchema));
    };
}
