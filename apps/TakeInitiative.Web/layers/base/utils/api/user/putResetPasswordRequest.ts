import type { AxiosInstance } from "axios";

export type ResetPasswordRequest = {
    email: string;
    password: string;
    token: string;
};

export function putResetPassword(axios: AxiosInstance) {
    return async function (request: ResetPasswordRequest) {
        return await axios.put("/api/resetPassword", request);
    };
}
