import { validateResponse } from "base/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import {
    type MaintenanceConfig,
    MaintenanceConfigValidator,
} from "../../../../../../base/utils/api/admin/getMaintainenceRequest";

export function putMaintenanceRequest(axios: AxiosInstance) {
    return (cfg: MaintenanceConfig): Promise<MaintenanceConfig> =>
        axios
            .put("/api/admin/maintenance", cfg)
            .then((resp) => validateResponse(resp, MaintenanceConfigValidator));
}
z;
