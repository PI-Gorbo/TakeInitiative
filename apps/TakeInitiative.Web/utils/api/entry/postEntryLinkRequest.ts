import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Entry Link (27c): a knowledge-base link (`kind: "KnowledgeBase"` with `provider` and
// `itemId`) or an external one (`kind: "External"` with `url` and `label`). Who may write is the
// entry's editors plus a claimed Character's player (27d); a bad url or label is a 400 with
// `errors.url` / `errors.label`, an unknown reference row a 404 `errors.itemId`, and a duplicate
// or the 21st link a 409. Answers the caller's whole view of the entry.
export type PostEntryLinkRequest = ApiPathParams<"PostEntryLink"> & ApiRequestBody<"PostEntryLink">;
export type PostEntryLinkResponse = ApiResponse<"PostEntryLink">;
export function postEntryLinkRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...body }: PostEntryLinkRequest): Promise<PostEntryLinkResponse> {
        const response = await axios.post<PostEntryLinkResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/links`,
            body satisfies ApiRequestBody<"PostEntryLink">
        );
        return response.data;
    };
}
