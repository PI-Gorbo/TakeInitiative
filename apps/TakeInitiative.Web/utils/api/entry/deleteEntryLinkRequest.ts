import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Delete Entry Link (27c): who may add may remove, and so may a DM. A link id that is not on the
// entry — or one the caller may not read — is a 404, so guessing an NPC's link id tells a player
// nothing. A stale knowledge-base link removes exactly like a live one. Answers the entry.
export type DeleteEntryLinkRequest = ApiPathParams<"DeleteEntryLink">;
export type DeleteEntryLinkResponse = ApiResponse<"DeleteEntryLink">;
export function deleteEntryLinkRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, linkId }: DeleteEntryLinkRequest): Promise<DeleteEntryLinkResponse> {
        const response = await axios.delete<DeleteEntryLinkResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/links/${encodeURIComponent(linkId)}`
        );
        return response.data;
    };
}
