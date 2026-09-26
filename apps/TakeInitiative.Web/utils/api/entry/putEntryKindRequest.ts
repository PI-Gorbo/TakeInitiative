import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Entry Kind (can edit).
export type PutEntryKindRequest = ApiPathParams<"PutEntryKind"> & ApiRequestBody<"PutEntryKind">;
export type PutEntryKindResponse = ApiResponse<"PutEntryKind">;
export function putEntryKindRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PutEntryKindRequest): Promise<PutEntryKindResponse> {
        const response = await axios.put<PutEntryKindResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/kind`,
            body satisfies ApiRequestBody<"PutEntryKind">
        );
        return response.data;
    };
}
