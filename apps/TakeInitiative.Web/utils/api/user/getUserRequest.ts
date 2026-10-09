import type { AxiosInstance } from "axios";
import type { ApiResponse } from "../types";

// Get User. The user's campaigns come from GET /api/campaigns.
export type GetUserResponse = ApiResponse<"GetUser">;
export function getUserRequest(axios: AxiosInstance) {
    return async function getUser(): Promise<GetUserResponse> {
        const response = await axios.get<GetUserResponse>("/api/user", { data: {} });
        return response.data;
    };
}
