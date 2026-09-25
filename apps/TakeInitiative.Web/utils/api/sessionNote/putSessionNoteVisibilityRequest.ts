import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Session Note Visibility (author).
export type PutSessionNoteVisibilityRequest = ApiPathParams<"PutSessionNoteVisibility"> &
    ApiRequestBody<"PutSessionNoteVisibility">;
export type PutSessionNoteVisibilityResponse = ApiResponse<"PutSessionNoteVisibility">;
export function putSessionNoteVisibilityRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        noteId,
        ...body
    }: PutSessionNoteVisibilityRequest): Promise<PutSessionNoteVisibilityResponse> {
        const response = await axios.put<PutSessionNoteVisibilityResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/notes/${encodeURIComponent(noteId)}/visibility`,
            body satisfies ApiRequestBody<"PutSessionNoteVisibility">
        );
        return response.data;
    };
}
