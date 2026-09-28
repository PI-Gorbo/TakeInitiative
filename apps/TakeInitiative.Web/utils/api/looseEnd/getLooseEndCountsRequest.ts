import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Loose End Counts (members): how many loose ends the caller has, in all and per
// session (19b). The same rules as the list, with no matcher call, so it is cheap.
export type GetLooseEndCountsRequest = ApiPathParams<"GetLooseEndCounts">;
export type GetLooseEndCountsResponse = ApiResponse<"GetLooseEndCounts">;
export function getLooseEndCountsRequest(axios: AxiosInstance) {
    return async function ({ campaignId }: GetLooseEndCountsRequest): Promise<GetLooseEndCountsResponse> {
        const response = await axios.get<GetLooseEndCountsResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/loose-ends/counts`
        );
        return response.data;
    };
}
