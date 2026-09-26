import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Session Title (DMs). A blank or null title clears it.
export type PutSessionTitleRequest = ApiPathParams<"PutSessionTitle"> & ApiRequestBody<"PutSessionTitle">;
export type PutSessionTitleResponse = ApiResponse<"PutSessionTitle">;
export function putSessionTitleRequest(axios: AxiosInstance) {
    return async function ({ campaignId, sessionId, ...body }: PutSessionTitleRequest): Promise<PutSessionTitleResponse> {
        const response = await axios.put<PutSessionTitleResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/sessions/${encodeURIComponent(sessionId)}/title`,
            body satisfies ApiRequestBody<"PutSessionTitle">
        );
        return response.data;
    };
}
