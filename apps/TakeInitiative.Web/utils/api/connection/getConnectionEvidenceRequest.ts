import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Connection Evidence (members): the blocks, notes and combats that connect two
// entries, cut from what the caller can see (19a). An unconnected pair is a 200 with
// no evidence.
export type GetConnectionEvidenceRequest =
    ApiPathParams<"GetConnectionEvidence">;
export type GetConnectionEvidenceResponse =
    ApiResponse<"GetConnectionEvidence">;
export function getConnectionEvidenceRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        entryId,
        otherEntryId,
    }: GetConnectionEvidenceRequest): Promise<GetConnectionEvidenceResponse> {
        const response = await axios.get<GetConnectionEvidenceResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/connections/${encodeURIComponent(otherEntryId)}`
        );
        return response.data;
    };
}
