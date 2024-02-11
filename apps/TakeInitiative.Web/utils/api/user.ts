import type { AxiosInstance, AxiosResponse } from "axios";
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
function getUserRequest(axios: AxiosInstance) {
    return async function getUser(): Promise<GetUserResponse> {
        return axios.get("/api/user").then(async function (response) {
            const result = await getUserResponseSchema.validate(response.data);
            return result;
        });
    };
}

// Sign Ups
export type SignUpRequest = {
    email: string;
    username: string;
    password: string;
};
const signUpResponseSchema = yup.object({
    token: yup.string().required(),
});
export type SignUpResponse = yup.InferType<typeof signUpResponseSchema>;
function signUpRequest(axios: AxiosInstance) {
    return async function signUp(
        signUpRequest: SignUpRequest,
    ): Promise<SignUpResponse> {
        return await axios
            .post("/api/signup", signUpRequest)
            .then((response) => signUpResponseSchema.validate(response.data));
    };
}

export const user = (axios: AxiosInstance) => {
    return {
        getUser: getUserRequest(axios),
        signUp: signUpRequest(axios),
    };
};
