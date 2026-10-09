import type { AxiosInstance } from "axios";
import type { ApiRequestBody } from "../types";

export type PutUsernameRequest = ApiRequestBody<"PutUsername">;
export function putUsername(axios: AxiosInstance) {
    return async function (request: PutUsernameRequest) {
        return await axios.put("/api/user/username", request);
    };
}
