import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Entry History (who can see the entry, 15g): every change, oldest first, redacted
// for the caller.
export type GetEntryHistoryRequest = ApiPathParams<"GetEntryHistory">;
export type GetEntryHistoryResponse = ApiResponse<"GetEntryHistory">;
export function getEntryHistoryRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId }: GetEntryHistoryRequest): Promise<GetEntryHistoryResponse> {
        const response = await axios.get<GetEntryHistoryResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/history`
        );
        return response.data;
    };
}
