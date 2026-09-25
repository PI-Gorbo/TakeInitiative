import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Entry Quote (can edit): promote a session note, or part of it, into the article.
// `text` left out quotes the whole note. Answers `{ entry, blockId }`.
export type PostEntryQuoteRequest = ApiPathParams<"PostEntryQuote"> & ApiRequestBody<"PostEntryQuote">;
export type PostEntryQuoteResponse = ApiResponse<"PostEntryQuote">;
export function postEntryQuoteRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PostEntryQuoteRequest): Promise<PostEntryQuoteResponse> {
        const response = await axios.post<PostEntryQuoteResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/quotes`,
            body satisfies ApiRequestBody<"PostEntryQuote">
        );
        return response.data;
    };
}
