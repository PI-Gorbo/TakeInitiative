import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Entry (members): the caller is its creator. A name that is already the name or
// an alias of an entry the caller can see is a 409 with `errors.existingEntryId`.
export type PostEntryRequest = ApiPathParams<"PostEntry"> & ApiRequestBody<"PostEntry">;
export type PostEntryResponse = ApiResponse<"PostEntry">;
export function postEntryRequest(axios: AxiosInstance) {
    return async function ({ campaignId, ...body }: PostEntryRequest): Promise<PostEntryResponse> {
        const response = await axios.post<PostEntryResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries`,
            body satisfies ApiRequestBody<"PostEntry">
        );
        return response.data;
    };
}
