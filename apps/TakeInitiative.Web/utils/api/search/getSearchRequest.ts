import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Search (members): ⌘K search (17a). `q` is 1–100 characters; `sections` is a comma
// list of entries, notes, images and sessions (default all); `take` is 1–20 per section.
// `signal` cancels a request that a newer keystroke has replaced.
export type GetSearchRequest = ApiPathParams<"GetSearch"> & {
    q: string;
    sections?: string;
    take?: number;
    signal?: AbortSignal;
};
export type GetSearchResponse = ApiResponse<"GetSearch">;
export function getSearchRequest(axios: AxiosInstance) {
    return async function ({ campaignId, signal, ...params }: GetSearchRequest): Promise<GetSearchResponse> {
        const response = await axios.get<GetSearchResponse>(`/api/campaigns/${encodeURIComponent(campaignId)}/search`, {
            params,
            signal,
        });
        return response.data;
    };
}
