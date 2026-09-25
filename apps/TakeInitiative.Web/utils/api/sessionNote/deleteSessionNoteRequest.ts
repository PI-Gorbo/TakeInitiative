import type { AxiosInstance } from "axios";
import type { ApiPathParams } from "../types";

// Delete Session Note (author). 204, no body.
export type DeleteSessionNoteRequest = ApiPathParams<"DeleteSessionNote">;
export function deleteSessionNoteRequest(axios: AxiosInstance) {
    return async function ({ campaignId, noteId }: DeleteSessionNoteRequest): Promise<void> {
        await axios.delete(`/api/campaigns/${encodeURIComponent(campaignId)}/notes/${encodeURIComponent(noteId)}`);
    };
}
