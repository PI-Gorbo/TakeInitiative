import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiRequestBody, ApiResponse } from "../types";

// Put Entry Primary Image (SAM-12): whoever may edit the entry. `imageId` null removes it. An
// image that is not in the entry's gallery, or that not everyone can see, is a 400 with
// `errors.imageId`.
export type PutEntryPrimaryImageRequest = ApiPathParams<"PutEntryPrimaryImage"> &
    ApiRequestBody<"PutEntryPrimaryImage">;
export type PutEntryPrimaryImageResponse = ApiResponse<"PutEntryPrimaryImage">;
export function putEntryPrimaryImageRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        entryId,
        ...body
    }: PutEntryPrimaryImageRequest): Promise<PutEntryPrimaryImageResponse> {
        const response = await axios.put<PutEntryPrimaryImageResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/primary-image`,
            body satisfies ApiRequestBody<"PutEntryPrimaryImage">
        );
        return response.data;
    };
}
