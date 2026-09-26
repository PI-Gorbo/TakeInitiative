import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Entry Claim (15g): `memberId` claims (the caller's own; any member's for a DM), null unclaims.
export type PutEntryClaimRequest = ApiPathParams<"PutEntryClaim"> & ApiRequestBody<"PutEntryClaim">;
export type PutEntryClaimResponse = ApiResponse<"PutEntryClaim">;
export function putEntryClaimRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PutEntryClaimRequest): Promise<PutEntryClaimResponse> {
        const response = await axios.put<PutEntryClaimResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/claim`,
            body satisfies ApiRequestBody<"PutEntryClaim">
        );
        return response.data;
    };
}
