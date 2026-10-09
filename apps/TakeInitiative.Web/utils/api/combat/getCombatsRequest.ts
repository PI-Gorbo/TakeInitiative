import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse, CombatStatus } from "../types";

// Get Combats (members): summaries of the combats the caller can see, newest first.
// `status` narrows them; the server takes a comma list. Players never get a Draft.
export type GetCombatsRequest = ApiPathParams<"GetCombats"> & {
    status?: readonly CombatStatus[];
};
export type GetCombatsResponse = ApiResponse<"GetCombats">;
export function getCombatsRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        status,
    }: GetCombatsRequest): Promise<GetCombatsResponse> {
        const response = await axios.get<GetCombatsResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/combats`,
            {
                params:
                    status && status.length > 0
                        ? { status: status.join(",") }
                        : undefined,
            }
        );
        return response.data;
    };
}
