import type { AxiosInstance } from "axios";
import * as yup from "yup";

// Get User
const getUserCampaignDto = yup.object({
    campaignName: yup.string().required(),
    campaignId: yup.string().required(),
});
const getUserResponseSchema = yup.object({
    userId: yup.string().required(),
    username: yup.string().required(),
    dmCampaigns: yup.array(getUserCampaignDto).required(),
    memberCampaigns: yup.array(getUserCampaignDto).required(),
});
export type GetUserResponse = yup.InferType<typeof getUserResponseSchema>;
export function getUserRequest(axios: AxiosInstance) {
    return async function getUser(): Promise<GetUserResponse> {
        return axios.get("/api/user").then(async function (response) {
            const result = await getUserResponseSchema.validate(response.data);
            return result;
        });
    };
}
