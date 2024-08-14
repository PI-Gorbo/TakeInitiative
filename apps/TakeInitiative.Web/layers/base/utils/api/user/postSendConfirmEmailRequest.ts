import { Body } from "../../../.nuxt/components";
import type { AxiosInstance } from "axios";
import * as yup from "yup";
import { getUserResponseSchema, type GetUserResponse } from "./getUserRequest";

export function postSendConfirmEmailRequest(axios: AxiosInstance) {
    return async function (): Promise<void> {
        return axios.post("/api/sendConfirmEmail");
    };
}
