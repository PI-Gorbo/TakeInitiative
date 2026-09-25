import type { AxiosInstance } from "axios";
import type { ApiRequestBody } from "../types";

export type ResetPasswordRequest = ApiRequestBody<"PutResetPassword">;

export function putResetPassword(axios: AxiosInstance) {
    return async function (request: ResetPasswordRequest) {
        return await axios.put("/api/resetPassword", request);
    };
}
