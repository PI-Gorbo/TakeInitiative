import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Entry Stats (15g): the claimer and the DMs, or the DMs only on an unclaimed
// Character. A bad dice expression is a 400 with `errors.initiativeRoll` or `errors.maxHp`.
export type PutEntryStatsRequest = ApiPathParams<"PutEntryStats"> & ApiRequestBody<"PutEntryStats">;
export type PutEntryStatsResponse = ApiResponse<"PutEntryStats">;
export function putEntryStatsRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PutEntryStatsRequest): Promise<PutEntryStatsResponse> {
        const response = await axios.put<PutEntryStatsResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/stats`,
            body satisfies ApiRequestBody<"PutEntryStats">
        );
        return response.data;
    };
}
