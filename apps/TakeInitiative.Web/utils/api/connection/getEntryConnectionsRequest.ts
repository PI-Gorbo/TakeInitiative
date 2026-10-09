import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Entry Connections (members): the entries connected to this one, as the caller
// sees them, heaviest first, each with its weight split by kind of evidence (19a).
export type GetEntryConnectionsRequest = ApiPathParams<"GetEntryConnections">;
export type GetEntryConnectionsResponse = ApiResponse<"GetEntryConnections">;
export function getEntryConnectionsRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        entryId,
    }: GetEntryConnectionsRequest): Promise<GetEntryConnectionsResponse> {
        const response = await axios.get<GetEntryConnectionsResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/connections`
        );
        return response.data;
    };
}
