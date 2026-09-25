import type { AxiosInstance } from "axios";
import { z } from "zod";

// Get User. The user's campaigns come from GET /api/campaigns.
export const getUserResponseSchema = z
    .object({
        userId: z.string(),
        username: z.string(),
        confirmedEmail: z.boolean(),
    })
    .required();
export type GetUserResponse = z.infer<typeof getUserResponseSchema>;
export function getUserRequest(axios: AxiosInstance) {
    return async function getUser(): Promise<GetUserResponse> {
        return axios
            .get("/api/user", { data: {} })
            .then((resp) => validateResponse(resp, getUserResponseSchema));
    };
}
