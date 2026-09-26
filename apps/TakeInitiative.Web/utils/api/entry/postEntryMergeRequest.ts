import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Entry Merge (can edit both, 15g): merges `entryId` into `intoEntryId`. A 409 with
// `errors.visibility`, `errors.claim`, `errors.aliases` or `errors.merged` explains a refusal.
export type PostEntryMergeRequest = ApiPathParams<"PostEntryMerge"> & ApiRequestBody<"PostEntryMerge">;
export type PostEntryMergeResponse = ApiResponse<"PostEntryMerge">;
export function postEntryMergeRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PostEntryMergeRequest): Promise<PostEntryMergeResponse> {
        const response = await axios.post<PostEntryMergeResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/merge`,
            body satisfies ApiRequestBody<"PostEntryMerge">
        );
        return response.data;
    };
}
