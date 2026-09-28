import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Entry Combats (members): the combats in which the entry, or one merged into it, is
// a combatant the caller can see, as cards, newest first (18e.4).
export type GetEntryCombatsRequest = ApiPathParams<"GetEntryCombats">;
export type GetEntryCombatsResponse = ApiResponse<"GetEntryCombats">;
export function getEntryCombatsRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        entryId,
    }: GetEntryCombatsRequest): Promise<GetEntryCombatsResponse> {
        const response = await axios.get<GetEntryCombatsResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/combats`
        );
        return response.data;
    };
}
