import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Entry Aliases (can edit): the whole list.
export type PutEntryAliasesRequest = ApiPathParams<"PutEntryAliases"> & ApiRequestBody<"PutEntryAliases">;
export type PutEntryAliasesResponse = ApiResponse<"PutEntryAliases">;
export function putEntryAliasesRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PutEntryAliasesRequest): Promise<PutEntryAliasesResponse> {
        const response = await axios.put<PutEntryAliasesResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/aliases`,
            body satisfies ApiRequestBody<"PutEntryAliases">
        );
        return response.data;
    };
}
