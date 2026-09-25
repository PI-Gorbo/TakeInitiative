import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Entries (members): every entry the caller can see, with the caller's own
// mention counts (15b). Ordered by name.
export type GetEntriesRequest = ApiPathParams<"GetEntries">;
export type GetEntriesResponse = ApiResponse<"GetEntries">;
export function getEntriesRequest(axios: AxiosInstance) {
    return async function ({ campaignId }: GetEntriesRequest): Promise<GetEntriesResponse> {
        const response = await axios.get<GetEntriesResponse>(`/api/campaigns/${encodeURIComponent(campaignId)}/entries`);
        return response.data;
    };
}
