import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Entry Timeline (who can see the entry): the notes the caller can see that
// mention it. The first call is the newest page; items are oldest first within a
// page. `before` is the first item's `note.postedAt`; `take` is 1–50 (default 20).
export type GetEntryTimelineRequest = ApiPathParams<"GetEntryTimeline"> & {
    before?: string;
    take?: number;
};
export type GetEntryTimelineResponse = ApiResponse<"GetEntryTimeline">;
export function getEntryTimelineRequest(axios: AxiosInstance) {
    return async function ({ campaignId, entryId, ...query }: GetEntryTimelineRequest): Promise<GetEntryTimelineResponse> {
        const response = await axios.get<GetEntryTimelineResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/entries/${encodeURIComponent(entryId)}/timeline`,
            { params: query }
        );
        return response.data;
    };
}
