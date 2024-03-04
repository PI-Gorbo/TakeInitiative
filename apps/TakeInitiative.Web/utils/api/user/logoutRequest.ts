import type { AxiosInstance, AxiosResponse } from "axios";
import * as yup from "yup";

export function logoutRequest(axios: AxiosInstance) {
    return async function (): Promise<void> {
        return await axios.post("/api/logout");
    };
}
