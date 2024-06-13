import type { AxiosInstance } from "axios";
import { yup } from "base/utils/types/HelperTypes";
import type { InferType } from "yup";

export const MaintenanceConfigValidator = yup.object({
    inMaintenanceMode: yup.boolean(),
    reason: yup.string().nullable(),
});
export type MaintenanceConfig = InferType<typeof MaintenanceConfigValidator>;

export function getMaintenanceRequest(axios: AxiosInstance) {
    return (): Promise<MaintenanceConfig> =>
        axios
            .get("/api/admin/maintenance")
            .then((resp) =>
                validateWithSchema(resp.data, MaintenanceConfigValidator),
            );
}
