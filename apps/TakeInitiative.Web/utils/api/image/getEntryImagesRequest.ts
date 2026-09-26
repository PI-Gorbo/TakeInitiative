import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Entry Images (who can see the entry): an entry's gallery, the image notes the
// caller can see whose caption mentions it, merged ids included (16d). Paged as the
// session gallery: `before` is the first item's `note.postedAt`; `take` is 1–60.
export type GetEntryImagesRequest = ApiPathParams<"GetEntryImages"> & {
    before?: string;
    take?: number;
};
export type GetEntryImagesResponse = ApiResponse<"GetEntryImages">;
export function getEntryImagesRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...query }: GetEntryImagesRequest): Promise<GetEntryImagesResponse> {
        const response = await axios.get<GetEntryImagesResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/images`,
            { params: query }
        );
        return response.data;
    };
}
