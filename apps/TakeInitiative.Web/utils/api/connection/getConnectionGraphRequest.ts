import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse, EntryKind } from "../types";

// Get Connection Graph (members): the campaign's connections as nodes and edges, as the
// caller sees them (19a). With `focus`, that entry and its neighbours to `depth` 1 or 2;
// without, every connected entry. `kinds` keeps nodes of those kinds (never dropping
// the focus). At most 300 nodes, with `truncated` set when more were cut.
export type GetConnectionGraphRequest = ApiPathParams<"GetConnectionGraph"> & {
    focus?: string | null;
    depth?: 1 | 2;
    /** Empty or absent: every kind. */
    kinds?: readonly EntryKind[];
};
export type GetConnectionGraphResponse = ApiResponse<"GetConnectionGraph">;
export function getConnectionGraphRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        focus,
        depth,
        kinds,
    }: GetConnectionGraphRequest): Promise<GetConnectionGraphResponse> {
        const params: Record<string, string> = {};
        if (focus) {
            params.focus = focus;
            if (depth) params.depth = String(depth);
        }
        if (kinds && kinds.length > 0) params.kinds = kinds.join(",");
        const response = await axios.get<GetConnectionGraphResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/connections/graph`,
            { params }
        );
        return response.data;
    };
}
