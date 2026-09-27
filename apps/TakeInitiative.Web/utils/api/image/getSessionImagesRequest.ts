import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Session Images (members): a session's gallery, the image notes the caller can
// see (16d). The first call is the newest page; items are oldest first within a page.
// `before` is the first item's `note.postedAt`; `take` is 1–60 (default 30).
export type GetSessionImagesRequest = ApiPathParams<"GetSessionImages"> & {
    before?: string;
    take?: number;
};
export type GetSessionImagesResponse = ApiResponse<"GetSessionImages">;
export function getSessionImagesRequest(axios: AxiosInstance) {
    return async function ({ campaignId, sessionId, ...query }: GetSessionImagesRequest): Promise<GetSessionImagesResponse> {
        const response = await axios.get<GetSessionImagesResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/sessions/${encodeURIComponent(sessionId)}/images`,
            { params: query }
        );
        return response.data;
    };
}
