import type { AxiosInstance, AxiosResponse } from "axios";

// Sign Ups
export type SignUpRequest = {
    email: string;
    username: string;
    password: string;
};

export function signUpRequest(axios: AxiosInstance) {
    return async function signUp(signUpRequest: SignUpRequest): Promise<void> {
        return await axios.post("/api/signup", signUpRequest);
    };
}
