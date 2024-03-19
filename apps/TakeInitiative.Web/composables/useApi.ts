import { createCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import { deleteCampaignRequest } from "~/utils/api/campaign/deleteCampaignRequest";

import { getCampaignRequest } from "~/utils/api/campaign/getCampaignRequest";
import { joinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";
import { updateCampaignDetailsRequest } from "~/utils/api/campaign/updateCampaignDetailsRequest";
import { deleteStagedCharacter } from "~/utils/api/combat/deleteStagedCharacterRequest";
import { getCombatRequest } from "~/utils/api/combat/getCombatRequest";
import { openCombatRequest } from "~/utils/api/combat/openCombatRequest";
import { postFinishCombatRequest } from "~/utils/api/combat/postFinishCombatRequest";
import { postStartCombatRequest } from "~/utils/api/combat/postStartCombat";
import { putUpsertStagedCharacter } from "~/utils/api/combat/putUpsertStagedCharacter";
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
import { logoutRequest } from "~/utils/api/user/logoutRequest";
import { signUpRequest } from "~/utils/api/user/signUpRequest";

export const useApi = () => {
    const { $axios } = useNuxtApp();
    return {
        user: {
            getUser: getUserRequest($axios),
            signUp: signUpRequest($axios),
            login: loginRequest($axios),
            logout: logoutRequest($axios),
        },
        campaign: {
            create: createCampaignRequest($axios),
            join: joinCampaignRequest($axios),
            update: updateCampaignDetailsRequest($axios),
            get: getCampaignRequest($axios),
            delete: deleteCampaignRequest($axios),
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
                    delete: deletePlannedCombatNpcRequest($axios),
                },
            },
        },
        combat: {
            start: postStartCombatRequest($axios),
            finish: postFinishCombatRequest($axios),
            open: openCombatRequest($axios),
            get: getCombatRequest($axios),
            stagedCharacters: {
                upsert: putUpsertStagedCharacter($axios),
                delete: deleteStagedCharacter($axios),
            },
        },
    };
};
