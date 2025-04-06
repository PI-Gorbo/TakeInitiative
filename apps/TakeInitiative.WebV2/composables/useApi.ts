import { createCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import { createPlayerCharacterRequest } from "~/utils/api/campaign/createPlayerCharacterRequest";
import { deleteCampaignRequest } from "~/utils/api/campaign/deleteCampaignRequest";
import { deletePlayerCharacterRequest } from "~/utils/api/campaign/deletePlayerCharacterRequest";

import { getCampaignRequest } from "~/utils/api/campaign/getCampaignRequest";
import { joinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";
import { updateCampaignDetailsRequest } from "~/utils/api/campaign/updateCampaignDetailsRequest";
import { updatePlayerCharacterRequest } from "~/utils/api/campaign/updatePlayerCharacterRequest";
import { deleteInitiativeCharacterRequest } from "~/utils/api/combat/deleteInitiativeCharacterRequest";
import { deleteStagedCharacter } from "~/utils/api/combat/deleteStagedCharacterRequest";
import { getCombatRequest } from "~/utils/api/combat/getCombatRequest";
import { openCombatRequest } from "~/utils/api/combat/openCombatRequest.js";
import { postFinishCombatRequest } from "~/utils/api/combat/postFinishCombatRequest";
import { postEndTurnRequest } from "~/utils/api/combat/postNextTurn";
import { postRollStagedCharactersIntoInitiativeRequest } from "~/utils/api/combat/postRollStagedCharactersIntoInitiative";
import { postStagedPlannedCharactersRequest } from "~/utils/api/combat/postStagePlannedCharactersRequest";
import { postStagePlayerCharactersRequest as postStagePlayerCharactersRequest } from "~/utils/api/combat/postStagePlayerCharactersRequest";
import { postStartCombatRequest } from "~/utils/api/combat/postStartCombat";
import { putUpdateInitiativeCharacterRequest } from "~/utils/api/combat/putUpdateInitiativeCharacterRequest";
import { putUpdateStagedCharacter } from "~/utils/api/combat/putUpsertStagedCharacter";
import { createPlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";
import { deletePlannedCombatRequest } from "~/utils/api/plannedCombat/deletePlannedCombatRequest";
import { getCombatsRequest } from "~/utils/api/combat/getCombatsRequest";
import { createPlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
import { deletePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/deletePlannedCombatStageRequest";
import { createPlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import { deletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
import { updatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import { updatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/updatePlannedCombatStageRequest";
import { getUserRequest } from "~/utils/api/user/getUserRequest";
import { loginRequest } from "~/utils/api/user/loginRequest";
import { logoutRequest } from "~/utils/api/user/logoutRequest";
import { signUpRequest } from "~/utils/api/user/signUpRequest";
import { postConfirmEmailRequest as confirmEmailRequest } from "~/utils/api/user/postConfirmEmailRequest";
import { postSendConfirmEmailRequest } from "~/utils/api/user/postSendConfirmEmailRequest";
import { putSendResetPasswordRequest } from "~/utils/api/user/putSendResetPasswordRequest";
import { putResetPassword } from "~/utils/api/user/putResetPasswordRequest";
import { getMaintenanceRequest } from "~/utils/api/admin/getMaintainenceRequest";
import { putCampaignMemberResourcesRequest } from "~/utils/api/campaign/putCampaignMemberResourcesRequest";
import { getCombatHistory } from "~/utils/api/combat/getCombatHistoryRequest";
import { postAddStagedCharacter } from "~/utils/api/combat/postAddStagedCharacter";
import { getPlannedCombatRequest } from "~/utils/api/plannedCombat/getPlannedCombatRequest";

export const useApi = () => {
    const { $axios } = useNuxtApp();
    return {
        user: {
            getUser: getUserRequest($axios),
            signUp: signUpRequest($axios),
            login: loginRequest($axios),
            logout: logoutRequest($axios),
            confirmEmailWithToken: confirmEmailRequest($axios),
            sendConfirmationEmail: postSendConfirmEmailRequest($axios),
            sendResetPasswordEmail: putSendResetPasswordRequest($axios),
            resetPasswordWithToken: putResetPassword($axios),
        },
        campaign: {
            create: createCampaignRequest($axios),
            join: joinCampaignRequest($axios),
            update: updateCampaignDetailsRequest($axios),
            get: getCampaignRequest($axios),
            delete: deleteCampaignRequest($axios),
            playerCharacters: {
                create: createPlayerCharacterRequest($axios),
                update: updatePlayerCharacterRequest($axios),
                delete: deletePlayerCharacterRequest($axios),
            },
            member: {
                setResources: putCampaignMemberResourcesRequest($axios),
            },
        },
        draftCombat: {
            create: createPlannedCombatRequest($axios),
            delete: deletePlannedCombatRequest($axios),
            get: getPlannedCombatRequest($axios),
            stage: {
                create: createPlannedCombatStageRequest($axios),
                delete: deletePlannedCombatStageRequest($axios),
                update: updatePlannedCombatStageRequest($axios),
                npc: {
                    create: createPlannedCombatNpcRequest($axios),
                    update: updatePlannedCombatNpcRequest($axios),
                    delete: deletePlannedCombatNpcRequest($axios),
                },
            },
        },
        combat: {
            getAll: getCombatsRequest($axios),
            get: getCombatRequest($axios),
            history: getCombatHistory($axios),
            start: postStartCombatRequest($axios),
            finish: postFinishCombatRequest($axios),
            open: openCombatRequest($axios),
            endTurn: postEndTurnRequest($axios),
            stage: {
                character: {
                    update: putUpdateStagedCharacter($axios),
                    add: postAddStagedCharacter($axios),
                    delete: deleteStagedCharacter($axios),
                },
                planned: postStagedPlannedCharactersRequest($axios),
                rollIntoInitiative:
                    postRollStagedCharactersIntoInitiativeRequest($axios),
                playerCharacters: postStagePlayerCharactersRequest($axios),
            },
            initiative: {
                character: {
                    update: putUpdateInitiativeCharacterRequest($axios),
                    delete: deleteInitiativeCharacterRequest($axios),
                },
            },
        },
        admin: {
            getMaintenance: getMaintenanceRequest($axios),
        },
    };
};
