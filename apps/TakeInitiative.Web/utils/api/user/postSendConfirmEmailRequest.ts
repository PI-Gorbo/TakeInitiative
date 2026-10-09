import type { AxiosInstance } from "axios";

export function postSendConfirmEmailRequest(axios: AxiosInstance) {
    return async function (): Promise<void> {
        await axios.post("/api/sendConfirmEmail");
    };
}
