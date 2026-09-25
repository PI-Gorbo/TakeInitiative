import type { AxiosInstance, AxiosResponse } from "axios";
import type { ApiRequestBody } from "../types";

export type LoginRequest = ApiRequestBody<"PutLogin">;

export function loginRequest(axios: AxiosInstance) {
    return async function (request: LoginRequest): Promise<AxiosResponse<unknown>> {
        return await axios.put("/api/login", request);
    };
}
