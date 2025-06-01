import { validateResponse } from "~/utils/apiErrorParser";
import type { AxiosInstance } from "axios";
import { z } from "zod";

export const MaintenanceConfigValidator = z.object({
    inMaintenanceMode: z.boolean(),
    reason: z.string().nullable(),
});
export type MaintenanceConfig = z.infer<typeof MaintenanceConfigValidator>;

export function getMaintenanceRequest(axios: AxiosInstance) {
    return (): Promise<MaintenanceConfig> =>
        axios
            .get("/api/admin/maintenance")
            .then((resp) => validateResponse(resp, MaintenanceConfigValidator));
}
