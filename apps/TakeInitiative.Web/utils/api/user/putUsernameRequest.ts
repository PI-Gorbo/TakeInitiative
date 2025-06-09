import type { AxiosInstance } from "axios";
import { z } from "zod";

export const putUsernameRequest = z
    .object({
        newUsername: z.string(),
    })
    .required();
export type PutUsernameRequest = z.infer<typeof putUsernameRequest>;
export function putUsername(axios: AxiosInstance) {
    return async function (request: PutUsernameRequest): Promise<PutUsernameRequest> {
        return axios.put("/api/user/username", request)
    };
}
