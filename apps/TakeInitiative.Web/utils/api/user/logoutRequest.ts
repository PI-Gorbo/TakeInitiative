import type { AxiosInstance } from "axios";

export function logoutRequest(axios: AxiosInstance) {
    return async function (): Promise<void> {
        await axios.post("/api/logout");
    };
}
