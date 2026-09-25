import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Entry Name (can edit). Mentions keep their own text (invariant 6).
export type PutEntryNameRequest = ApiPathParams<"PutEntryName"> & ApiRequestBody<"PutEntryName">;
export type PutEntryNameResponse = ApiResponse<"PutEntryName">;
export function putEntryNameRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PutEntryNameRequest): Promise<PutEntryNameResponse> {
        const response = await axios.put<PutEntryNameResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/name`,
            body satisfies ApiRequestBody<"PutEntryName">
        );
        return response.data;
    };
}
