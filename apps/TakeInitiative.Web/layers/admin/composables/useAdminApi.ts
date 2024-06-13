import { getMaintenanceRequest } from "~/utils/api/admin/config/maintainence/getMaintainenceRequest";
import { putMaintenanceRequest } from "~/utils/api/admin/config/maintainence/putMaintainenceRequest";

export const useAdminApi = () => {
    const { $axios } = useNuxtApp();
    return {
        getMaintenance: getMaintenanceRequest($axios),
        putMaintenance: putMaintenanceRequest($axios),
    };
};
