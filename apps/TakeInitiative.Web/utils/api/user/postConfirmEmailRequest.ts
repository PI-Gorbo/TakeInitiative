import type { AxiosInstance } from "axios";
import type { ApiRequestBody, ApiResponse } from "../types";

export type ConfirmEmailRequest = ApiRequestBody<"PostConfirmEmail">;
export type ConfirmEmailResponse = ApiResponse<"PostConfirmEmail">;
export function postConfirmEmailRequest(axios: AxiosInstance) {
    return async function (code: string): Promise<ConfirmEmailResponse> {
        const request: ConfirmEmailRequest = { confirmEmailToken: code };
        const response = await axios.post<ConfirmEmailResponse>("/api/confirmEmail", request);
        return response.data;
    };
}
