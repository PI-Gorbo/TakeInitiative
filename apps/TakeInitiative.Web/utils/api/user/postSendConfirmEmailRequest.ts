import type { AxiosInstance } from "axios";

export function postSendConfirmEmailRequest(axios: AxiosInstance) {
    return async function (): Promise<void> {
        return axios.post("/api/sendConfirmEmail");
    };
}
