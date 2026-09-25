import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Session Note (author): edit the text and the recap flag.
export type PutSessionNoteRequest = ApiPathParams<"PutSessionNote"> & ApiRequestBody<"PutSessionNote">;
export type PutSessionNoteResponse = ApiResponse<"PutSessionNote">;
export function putSessionNoteRequest(axios: AxiosInstance) {
    return async function ({ campaignId, noteId, ...body }: PutSessionNoteRequest): Promise<PutSessionNoteResponse> {
        const response = await axios.put<PutSessionNoteResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/notes/${encodeURIComponent(noteId)}`,
            body satisfies ApiRequestBody<"PutSessionNote">
        );
        return response.data;
    };
}
