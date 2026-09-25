import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Start Session (members). `number` is the number the caller expects to start:
// current + 1 starts it, current returns the one someone else just started.
export type PostStartSessionRequest = ApiPathParams<"PostStartSession"> & ApiRequestBody<"PostStartSession">;
export type PostStartSessionResponse = ApiResponse<"PostStartSession">;
export function postStartSessionRequest(axios: AxiosInstance) {
    return async function ({ campaignId, ...body }: PostStartSessionRequest): Promise<PostStartSessionResponse> {
        const response = await axios.post<PostStartSessionResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/sessions`,
            body satisfies ApiRequestBody<"PostStartSession">
        );
        return response.data;
    };
}
