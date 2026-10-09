import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Post Entry From Reference (members, 20b), which is + Wiki: an entry of the item's
// suggested kind with its `Source`, and for a caller who could write its Stats (a DM, on a
// new NPC) the item's Stats in the same save. The duplicate-name rule is Post Entry's: a
// 409 with `errors.existingEntryId`. An unknown provider or item is a 404.
export type PostEntryFromReferenceRequest = ApiPathParams<"PostEntryFromReference"> &
    ApiRequestBody<"PostEntryFromReference">;
export type PostEntryFromReferenceResponse = ApiResponse<"PostEntryFromReference">;
export function postEntryFromReferenceRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        ...body
    }: PostEntryFromReferenceRequest): Promise<PostEntryFromReferenceResponse> {
        const response = await axios.post<PostEntryFromReferenceResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/from-reference`,
            body satisfies ApiRequestBody<"PostEntryFromReference">
        );
        return response.data;
    };
}
