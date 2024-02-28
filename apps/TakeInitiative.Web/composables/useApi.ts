import { createCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import { getCampaignRequest } from "~/utils/api/campaign/getCampaignRequest";
import { joinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";
import { updateCampaignDetailsRequest } from "~/utils/api/campaign/updateCampaignDetailsRequest";
import { createPlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";
import { deletePlannedCombatRequest } from "~/utils/api/plannedCombat/deletePlannedCombatRequest";
import { getPlannedCombatsRequest } from "~/utils/api/plannedCombat/getPlannedCombatsRequest";
import { createPlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
import { deletePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/deletePlannedCombatStageRequest";
import { createPlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import { deletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
import { updatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import { getUserRequest } from "~/utils/api/user/getUserRequest";
import { loginRequest } from "~/utils/api/user/loginRequest";
import { signUpRequest } from "~/utils/api/user/signUpRequest";

export const useApi = () => {
    const { $axios } = useNuxtApp();
    return {
        user: {
            getUser: getUserRequest($axios),
            signUp: signUpRequest($axios),
            login: loginRequest($axios),
        },
        campaign: {
            create: createCampaignRequest($axios),
            join: joinCampaignRequest($axios),
            update: updateCampaignDetailsRequest($axios),
            get: getCampaignRequest($axios),
        },
        plannedCombat: {
            create: createPlannedCombatRequest($axios),
            getAll: getPlannedCombatsRequest($axios),
            delete: deletePlannedCombatRequest($axios),
            stage: {
                create: createPlannedCombatStageRequest($axios),
                delete: deletePlannedCombatStageRequest($axios),
				npc: {
					create: createPlannedCombatNpcRequest($axios),
					update: updatePlannedCombatNpcRequest($axios),
					delete: deletePlannedCombatNpcRequest($axios)
				}
            },
        },
    };
};
