import type { AxiosInstance } from "axios";
import type { ApiResponse } from "../types";

export type MaintenanceConfig = ApiResponse<"GetMaintenanceConfig">;

export function getMaintenanceRequest(axios: AxiosInstance) {
    return (): Promise<MaintenanceConfig> =>
        axios.get<MaintenanceConfig>("/api/admin/maintenance").then((resp) => resp.data);
}
