import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse, SessionStreamFilter } from "../types";

// Get Session Stream (members): up to `take` sessions numbered below `before`, each
// with the notes the caller can see that match `filter`. No `before` ends at the
// current session.
export type GetSessionStreamRequest = ApiPathParams<"GetSessionStream"> & {
    filter?: SessionStreamFilter;
    before?: number;
    take?: number;
};
export type GetSessionStreamResponse = ApiResponse<"GetSessionStream">;
export function getSessionStreamRequest(axios: AxiosInstance) {
    return async function ({ campaignId, ...query }: GetSessionStreamRequest): Promise<GetSessionStreamResponse> {
        const response = await axios.get<GetSessionStreamResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/stream`,
            { params: query }
        );
        return response.data;
    };
}
