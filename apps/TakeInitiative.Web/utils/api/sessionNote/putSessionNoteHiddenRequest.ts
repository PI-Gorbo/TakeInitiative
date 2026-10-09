import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Session Note Hidden (DMs): hide or unhide a note.
export type PutSessionNoteHiddenRequest = ApiPathParams<"PutSessionNoteHidden"> & ApiRequestBody<"PutSessionNoteHidden">;
export type PutSessionNoteHiddenResponse = ApiResponse<"PutSessionNoteHidden">;
export function putSessionNoteHiddenRequest(axios: AxiosInstance) {
    return async function ({ campaignId, noteId, ...body }: PutSessionNoteHiddenRequest): Promise<PutSessionNoteHiddenResponse> {
        const response = await axios.put<PutSessionNoteHiddenResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/notes/${encodeURIComponent(noteId)}/hidden`,
            body satisfies ApiRequestBody<"PutSessionNoteHidden">
        );
        return response.data;
    };
}
