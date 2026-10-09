import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Sessions (members): every session, newest first, plus the gap prompt flag.
export type GetSessionsRequest = ApiPathParams<"GetSessions">;
export type GetSessionsResponse = ApiResponse<"GetSessions">;
export function getSessionsRequest(axios: AxiosInstance) {
    return async function ({ campaignId }: GetSessionsRequest): Promise<GetSessionsResponse> {
        const response = await axios.get<GetSessionsResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/sessions`
        );
        return response.data;
    };
}
