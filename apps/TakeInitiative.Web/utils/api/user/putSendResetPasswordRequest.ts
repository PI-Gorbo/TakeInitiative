import type { AxiosInstance } from "axios";
import type { ApiRequestBody } from "../types";

export type SendResetPasswordEmailRequest = ApiRequestBody<"PutSendResetPasswordEmail">;

export function putSendResetPasswordRequest(axios: AxiosInstance) {
    return async function (email: string) {
        const request: SendResetPasswordEmailRequest = { email };
        return await axios.put("/api/sendResetPasswordEmail", request);
    };
}
