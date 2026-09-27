import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Session Note History (who can see it): every version, oldest first.
export type GetSessionNoteHistoryRequest = ApiPathParams<"GetSessionNoteHistory">;
export type GetSessionNoteHistoryResponse = ApiResponse<"GetSessionNoteHistory">;
export function getSessionNoteHistoryRequest(axios: AxiosInstance) {
    return async function ({ campaignId, noteId }: GetSessionNoteHistoryRequest): Promise<GetSessionNoteHistoryResponse> {
        const response = await axios.get<GetSessionNoteHistoryResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/notes/${encodeURIComponent(noteId)}/history`
        );
        return response.data;
    };
}
