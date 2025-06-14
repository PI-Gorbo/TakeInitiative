import type { AxiosInstance } from "axios";
import { getUserResponseSchema, type GetUserResponse } from "./getUserRequest";

export type ConfirmEmailRequest = {};
export function postConfirmEmailRequest(axios: AxiosInstance) {
    return async function (code: string): Promise<GetUserResponse> {
        return axios
            .post("/api/confirmEmail", {
                ConfirmEmailToken: code,
            })
            .then((resp) => validateResponse(resp, getUserResponseSchema));
    };
}
