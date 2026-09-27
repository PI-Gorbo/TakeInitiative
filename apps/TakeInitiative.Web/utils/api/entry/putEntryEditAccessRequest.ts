import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Entry Edit Access (the creator and DMs).
export type PutEntryEditAccessRequest = ApiPathParams<"PutEntryEditAccess"> & ApiRequestBody<"PutEntryEditAccess">;
export type PutEntryEditAccessResponse = ApiResponse<"PutEntryEditAccess">;
export function putEntryEditAccessRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PutEntryEditAccessRequest): Promise<PutEntryEditAccessResponse> {
        const response = await axios.put<PutEntryEditAccessResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/edit-access`,
            body satisfies ApiRequestBody<"PutEntryEditAccess">
        );
        return response.data;
    };
}
