import { createCampaignRequest } from "base/utils/api/campaign/createCampaignRequest";
import { createPlayerCharacterRequest } from "base/utils/api/campaign/createPlayerCharacterRequest";
import { deleteCampaignRequest } from "base/utils/api/campaign/deleteCampaignRequest";
import { deletePlayerCharacterRequest } from "base/utils/api/campaign/deletePlayerCharacterRequest";

import { getCampaignRequest } from "base/utils/api/campaign/getCampaignRequest";
import { joinCampaignRequest } from "base/utils/api/campaign/joinCampaignRequest";
import { updateCampaignDetailsRequest } from "base/utils/api/campaign/updateCampaignDetailsRequest";
import { updatePlayerCharacterRequest } from "base/utils/api/campaign/updatePlayerCharacterRequest";
import { deleteInitiativeCharacterRequest } from "base/utils/api/combat/deleteInitiativeCharacterRequest";
import { deleteStagedCharacter } from "base/utils/api/combat/deleteStagedCharacterRequest";
import { getCombatRequest } from "base/utils/api/combat/getCombatRequest";
import { openCombatRequest } from "base/utils/api/combat/openCombatRequest";
import { postFinishCombatRequest } from "base/utils/api/combat/postFinishCombatRequest";
import { postEndTurnRequest } from "base/utils/api/combat/postNextTurn";
import { postRollStagedCharactersIntoInitiativeRequest } from "base/utils/api/combat/postRollStagedCharactersIntoInitiative";
import { postStagedPlannedCharactersRequest } from "base/utils/api/combat/postStagePlannedCharactersRequest";
import { postStagePlayerCharactersRequest as postStagePlayerCharactersRequest } from "base/utils/api/combat/postStagePlayerCharactersRequest";
import { postStartCombatRequest } from "base/utils/api/combat/postStartCombat";
import { putUpdateInitiativeCharacterRequest } from "base/utils/api/combat/putUpdateInitiativeCharacterRequest";
import { putUpsertStagedCharacter } from "base/utils/api/combat/putUpsertStagedCharacter";
import { createPlannedCombatRequest } from "base/utils/api/plannedCombat/createPlannedCombatRequest";
import { deletePlannedCombatRequest } from "base/utils/api/plannedCombat/deletePlannedCombatRequest";
import { getCombatsRequest } from "base/utils/api/combat/getCombatsRequest";
import { createPlannedCombatStageRequest } from "base/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
import { deletePlannedCombatStageRequest } from "base/utils/api/plannedCombat/stages/deletePlannedCombatStageRequest";
import { createPlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import { deletePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
import { updatePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import { updatePlannedCombatStageRequest } from "base/utils/api/plannedCombat/stages/updatePlannedCombatStageRequest";
import { getUserRequest } from "base/utils/api/user/getUserRequest";
import { loginRequest } from "base/utils/api/user/loginRequest";
import { logoutRequest } from "base/utils/api/user/logoutRequest";
import { signUpRequest } from "base/utils/api/user/signUpRequest";
import { postConfirmEmailRequest as confirmEmailRequest } from "base/utils/api/user/postConfirmEmailRequest";
import { postSendConfirmEmailRequest } from "base/utils/api/user/postSendConfirmEmailRequest";
import { putSendResetPasswordRequest } from "base/utils/api/user/putSendResetPasswordRequest";
import { putResetPassword } from "base/utils/api/user/putResetPasswordRequest";

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
        },
        plannedCombat: {
            create: createPlannedCombatRequest($axios),
            delete: deletePlannedCombatRequest($axios),
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
            start: postStartCombatRequest($axios),
            finish: postFinishCombatRequest($axios),
            open: openCombatRequest($axios),
            endTurn: postEndTurnRequest($axios),
            stage: {
                character: {
                    upsert: putUpsertStagedCharacter($axios),
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
    };
};
