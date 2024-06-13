import type { AxiosInstance } from "axios";

export function putReadAndSaveCampaignsRequest(axios: AxiosInstance) {
    return () => axios.put("/api/admin/readAndSave/campaigns");
}
