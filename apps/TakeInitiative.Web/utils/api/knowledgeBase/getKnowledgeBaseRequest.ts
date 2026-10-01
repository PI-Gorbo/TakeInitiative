import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse, ReferenceCategory } from "../types";

// Get Knowledge base (members, 26e): a page of the ingested reference corpus, filtered by
// category, source book and corpus, optionally searched, with the total and the facet counts.
// The campaign in the route is there for membership only — the rows are global and the same
// for every campaign and every member. An empty corpus is a 200 with a total of zero.
// `take` is capped at 50 by the API; `skip` at 100,000.
export type GetKnowledgeBaseRequest = ApiPathParams<"GetKnowledgeBase"> & {
    q?: string;
    category?: ReferenceCategory;
    book?: string;
    provider?: string;
    skip?: number;
    take?: number;
    signal?: AbortSignal;
};
export type GetKnowledgeBaseResponse = ApiResponse<"GetKnowledgeBase">;
export function getKnowledgeBaseRequest(axios: AxiosInstance) {
    return async function ({ campaignId, signal, ...params }: GetKnowledgeBaseRequest): Promise<GetKnowledgeBaseResponse> {
        const response = await axios.get<GetKnowledgeBaseResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/knowledge-base`,
            { params, signal }
        );
        return response.data;
    };
}
