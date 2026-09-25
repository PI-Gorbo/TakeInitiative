import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Session Note (members). No `sessionId` posts to the current session.
export type PostSessionNoteRequest = ApiPathParams<"PostSessionNote"> & ApiRequestBody<"PostSessionNote">;
export type PostSessionNoteResponse = ApiResponse<"PostSessionNote">;
export function postSessionNoteRequest(axios: AxiosInstance) {
    return async function ({ campaignId, ...body }: PostSessionNoteRequest): Promise<PostSessionNoteResponse> {
        const response = await axios.post<PostSessionNoteResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/notes`,
            body satisfies ApiRequestBody<"PostSessionNote">
        );
        return response.data;
    };
}
