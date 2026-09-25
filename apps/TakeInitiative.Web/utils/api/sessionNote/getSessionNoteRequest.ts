import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Session Note (who can see it): the note and its session number, for deep links.
export type GetSessionNoteRequest = ApiPathParams<"GetSessionNote">;
export type GetSessionNoteResponse = ApiResponse<"GetSessionNote">;
export function getSessionNoteRequest(axios: AxiosInstance) {
    return async function ({ campaignId, noteId }: GetSessionNoteRequest): Promise<GetSessionNoteResponse> {
        const response = await axios.get<GetSessionNoteResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/notes/${encodeURIComponent(noteId)}`
        );
        return response.data;
    };
}
