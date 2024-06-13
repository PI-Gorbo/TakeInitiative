import { getMaintenanceRequest } from "base/utils/api/admin/getMaintainenceRequest";
import { putMaintenanceRequest } from "~/utils/api/admin/config/maintainence/putMaintainenceRequest";

export const useAdminApi = () => {
    const { $axios } = useNuxtApp();
    return {
        putMaintenance: putMaintenanceRequest($axios),
    };
};
