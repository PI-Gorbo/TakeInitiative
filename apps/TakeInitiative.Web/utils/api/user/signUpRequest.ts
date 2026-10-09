import type { AxiosInstance } from "axios";
import type { ApiRequestBody } from "../types";

// Sign Up
export type SignUpRequest = ApiRequestBody<"PostSignUp">;

export function signUpRequest(axios: AxiosInstance) {
    return async function signUp(request: SignUpRequest): Promise<void> {
        await axios.post("/api/signup", request);
    };
}
