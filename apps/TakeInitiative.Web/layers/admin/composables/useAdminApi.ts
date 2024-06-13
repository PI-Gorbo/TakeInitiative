import { getMaintenanceRequest } from "base/utils/api/admin/getMaintainenceRequest";
import { putMaintenanceRequest } from "~/utils/api/admin/config/maintainence/putMaintainenceRequest";
import { putReadAndSaveCampaignsRequest } from "~/utils/api/admin/readAndSave/putReadAndSaveCampaignsRequest";
import { putReprojectCombatsRequest } from "~/utils/api/admin/reproject/putReprojectCombatRequest";

export const useAdminApi = () => {
    const { $axios } = useNuxtApp();
    return {
        getMaintenance: getMaintenanceRequest($axios),
        putMaintenance: putMaintenanceRequest($axios),
        reproject: {
            combat: putReprojectCombatsRequest($axios),
        },
        readAndSave: {
            campaigns: putReadAndSaveCampaignsRequest($axios),
        },
    };
};
