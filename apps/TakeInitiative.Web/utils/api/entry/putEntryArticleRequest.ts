import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Entry Article (can edit). The caller's whole view of the article after the edit,
// in order, with the etag it was loaded with. A stale etag is a 409 (`errors.etag`).
export type PutEntryArticleRequest = ApiPathParams<"PutEntryArticle"> & ApiRequestBody<"PutEntryArticle">;
export type PutEntryArticleResponse = ApiResponse<"PutEntryArticle">;
export function putEntryArticleRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PutEntryArticleRequest): Promise<PutEntryArticleResponse> {
        const response = await axios.put<PutEntryArticleResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/article`,
            body satisfies ApiRequestBody<"PutEntryArticle">
        );
        return response.data;
    };
}
