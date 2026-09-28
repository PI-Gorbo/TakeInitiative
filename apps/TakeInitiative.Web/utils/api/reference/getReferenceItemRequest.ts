import type { AxiosInstance } from "axios";
import type { ApiResponse } from "../types";

// Get Reference Item (any signed-in user, 20b): one reference item with its stat block and
// the source's attribution. It is the same for every campaign, so the route has none. An
// unknown provider or id, or a search-only provider, is a 404.
export type GetReferenceItemRequest = { provider: string; itemId: string };
export type GetReferenceItemResponse = ApiResponse<"GetReferenceItem">;
export function getReferenceItemRequest(axios: AxiosInstance) {
    return async function ({ provider, itemId }: GetReferenceItemRequest): Promise<GetReferenceItemResponse> {
        const response = await axios.get<GetReferenceItemResponse>(
            `/api/reference/${encodeURIComponent(provider)}/${encodeURIComponent(itemId)}`
        );
        return response.data;
    };
}
