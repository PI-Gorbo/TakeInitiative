import type { AxiosInstance, AxiosResponse } from "axios";
import * as yup from "yup";

export type LoginRequest = {
    email: string;
    password: string;
};

export function loginRequest(axios: AxiosInstance) {
    return async function (request: LoginRequest): Promise<void> {
        return await axios.put("/api/login", request);
    };
}
