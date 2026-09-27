import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Entry Visibility (the creator and DMs).
export type PutEntryVisibilityRequest = ApiPathParams<"PutEntryVisibility"> & ApiRequestBody<"PutEntryVisibility">;
export type PutEntryVisibilityResponse = ApiResponse<"PutEntryVisibility">;
export function putEntryVisibilityRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PutEntryVisibilityRequest): Promise<PutEntryVisibilityResponse> {
        const response = await axios.put<PutEntryVisibilityResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/visibility`,
            body satisfies ApiRequestBody<"PutEntryVisibility">
        );
        return response.data;
    };
}
