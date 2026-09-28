import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Get Loose Ends (members): the caller's own loose ends (19b), notes newest first with
// their link suggestions, then entries by mention count. `sessionId` narrows it to one
// session's.
export type GetLooseEndsRequest = ApiPathParams<"GetLooseEnds"> & { sessionId?: string | null };
export type GetLooseEndsResponse = ApiResponse<"GetLooseEnds">;
export function getLooseEndsRequest(axios: AxiosInstance) {
    return async function ({ campaignId, sessionId }: GetLooseEndsRequest): Promise<GetLooseEndsResponse> {
        const response = await axios.get<GetLooseEndsResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/loose-ends`,
            { params: sessionId ? { sessionId } : {} }
        );
        return response.data;
    };
}
