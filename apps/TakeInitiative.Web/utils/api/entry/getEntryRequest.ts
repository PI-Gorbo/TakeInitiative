import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Entry (who can see it). An entry the caller cannot see is a 404.
export type GetEntryRequest = ApiPathParams<"GetEntry">;
export type GetEntryResponse = ApiResponse<"GetEntry">;
export function getEntryRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId }: GetEntryRequest): Promise<GetEntryResponse> {
        const response = await axios.get<GetEntryResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}`
        );
        return response.data;
    };
}
