import type { AxiosInstance, AxiosResponse } from "axios";
import * as yup from "yup";

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
export function signUpRequest(axios: AxiosInstance) {
    return async function signUp(
        signUpRequest: SignUpRequest,
    ): Promise<SignUpResponse> {
        return await axios
            .post("/api/signup", signUpRequest)
            .then((response) => signUpResponseSchema.validate(response.data));
    };
}