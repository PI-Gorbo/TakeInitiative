import type { AxiosInstance } from "axios";

export type DeleteCampaignRequest = {
    campaignId: string;
};
export function deleteCampaignRequest(axios: AxiosInstance) {
    return async function (request: DeleteCampaignRequest): Promise<void> {
        return await axios.delete("/api/campaign", { data: request });
    };
}
