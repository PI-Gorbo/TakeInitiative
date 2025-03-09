import type { AxiosInstance, AxiosResponse } from "axios";

export function logoutRequest(axios: AxiosInstance) {
    return async function (): Promise<void> {
        return await axios.post("/api/logout");
    };
}
