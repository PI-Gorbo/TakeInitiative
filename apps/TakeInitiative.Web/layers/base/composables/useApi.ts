import type {
    DeleteCampaignRequest,
    DeleteInitiativeCharacterRequest,
    DeletePlannedCombatNpcRequest,
    DeletePlannedCombatRequest,
    DeletePlannedCombatStageRequest,
    DeletePlayerCharacterRequest,
    DeleteStagedCharacterRequest,
    GetCombatRequest,
    JoinCampaignByJoinCodeRequest,
    PostCreateCampaignRequest,
    PostEndTurnRequest,
    PostFinishCombatRequest,
    PostOpenCombatRequest,
    PostPlannedCombatNpcRequest,
    PostPlannedCombatRequest,
    PostPlayerCharacterRequest,
    PostRollStagedCharactersIntoInitiativeRequest,
    PostSignUpRequest,
    PostStagePlayerCharactersRequest,
    PostStartCombatRequest,
    PutCampaignDetailsRequest,
    PutCampaignMemberResourcesRequest,
    PutLoginRequest,
    PutPlannedCombatNpcRequest,
    PutPlannedCombatStageRequest,
    PutPlayerCharacterRequest,
    PutResetPasswordRequest,
    PutSendResetPasswordEmailRequest,
    PutStagePlannedCharactersRequest,
    PutUpdateInitiativeCharacterRequest,
    PutUpsertStagedCharacterRequest,
} from "base/utils/api/api";
import type { AxiosResponse } from "axios";

function getData<TResponse>(resp: AxiosResponse<TResponse, any>) {
    return resp.data;
}

export const useApi = () => {
    const { $axios, $api: client } = useNuxtApp();
    return {
        user: {
            getUser: () =>
                client.api
                    .takeInitiativeApiFeaturesUsersGetUser()
                    .then(getData),
            signUp: (req: PostSignUpRequest) =>
                client.api
                    .takeInitiativeApiFeaturesUsersPostSignUp(req)
                    .then(getData),
            login: (req: PutLoginRequest) =>
                client.api
                    .takeInitiativeApiFeaturesUsersPutLogin(req)
                    .then(getData),
            logout: () => client.api.takeInitiativeApiFeaturesUsersPostLogout(),
            confirmEmailWithToken: (code: string) =>
                client.api
                    .takeInitiativeApiFeaturesUsersPostConfirmEmail({
                        confirmEmailToken: code,
                    })
                    .then(getData),
            sendConfirmationEmail: () =>
                client.api
                    .takeInitiativeApiFeaturesUsersPostSendConfirmEmail()
                    .then(getData),
            sendResetPasswordEmail: (req: PutSendResetPasswordEmailRequest) =>
                client.api
                    .takeInitiativeApiFeaturesUsersPutSendResetPasswordEmail(
                        req,
                    )
                    .then(getData),
            resetPasswordWithToken: (req: PutResetPasswordRequest) =>
                client.api
                    .takeInitiativeApiFeaturesUsersPutResetPassword(req)
                    .then(getData),
        },
        campaign: {
            create: (req: PostCreateCampaignRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCampaignsPostCreateCampaign(req)
                    .then(getData),
            join: (req: JoinCampaignByJoinCodeRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCampaignsPostJoinCampaign(req)
                    .then(getData),
            update: (req: PutCampaignDetailsRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCampaignsPutCampaignDetails(req)
                    .then(getData),
            get: (req: string) =>
                client.api
                    .takeInitiativeApiFeaturesCampaignsGetCampaign(req, {
                        campaignId: req,
                    })
                    .then(getData),
            delete: (req: DeleteCampaignRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCampaignsDeleteCampaign(req)
                    .then(getData),
            playerCharacters: {
                create: (req: PostPlayerCharacterRequest) =>
                    client.api
                        .takeInitiativeApiFeaturesCampaignsPostPlayerCharacter(
                            req,
                        )
                        .then(getData),
                update: (req: PutPlayerCharacterRequest) =>
                    client.api
                        .takeInitiativeApiFeaturesCampaignsPutPlayerCharacter(
                            req,
                        )
                        .then(getData),
                delete: (req: DeletePlayerCharacterRequest) =>
                    client.api
                        .takeInitiativeApiFeaturesCampaignsDeletePlayerCharacter(
                            req,
                        )
                        .then(getData),
            },
            member: {
                setResources: (req: PutCampaignMemberResourcesRequest) =>
                    client.api
                        .takeInitiativeApiFeaturesCampaignsPutCampaignMemberResources(
                            req,
                        )
                        .then(getData),
            },
        },
        plannedCombat: {
            create: (req: PostPlannedCombatRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCombatsPostPlannedCombat(req)
                    .then(getData),
            delete: (req: DeletePlannedCombatRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCombatsDeletePlannedCombat(req)
                    .then(getData),
            stage: {
                create: (req: PutPlannedCombatStageRequest) =>
                    client.api
                        .takeInitiativeApiFeaturesCombatsPostPlannedCombatStage(
                            req,
                        )
                        .then(getData),
                delete: (req: DeletePlannedCombatStageRequest) =>
                    client.api
                        .takeInitiativeApiFeaturesCombatsDeletePlannedCombatStage(
                            req,
                        )
                        .then(getData),
                update: (req: PutPlannedCombatStageRequest) =>
                    client.api
                        .takeInitiativeApiFeaturesCombatsPutPlannedCombatStage(
                            req,
                        )
                        .then(getData),
                npc: {
                    create: (req: PostPlannedCombatNpcRequest) =>
                        client.api
                            .takeInitiativeApiFeaturesCombatsPostPlannedCombatNpc(
                                req,
                            )
                            .then(getData),
                    update: (req: PutPlannedCombatNpcRequest) =>
                        client.api
                            .takeInitiativeApiFeaturesCombatsPutPlannedCombatNpc(
                                req,
                            )
                            .then(getData),
                    delete: (req: DeletePlannedCombatNpcRequest) =>
                        client.api
                            .takeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc(
                                req,
                            )
                            .then(getData),
                },
            },
        },
        combat: {
            getAll: () =>
                client.api
                    .takeInitiativeApiFeaturesCombatsGetCombats()
                    .then(getData),
            get: (req: GetCombatRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCombatsGetCombat(req.id!, req)
                    .then(getData),
            getHistory: (r: GetCombatRequest) =>
                client.api.takeInitiativeApiFeaturesCombatsGetCombatHistory(
                    r.id!,
                    r,
                ),
            start: (req: PostStartCombatRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCombatsPostStartCombat(req)
                    .then(getData),
            finish: (req: PostFinishCombatRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCombatsPostFinishCombat(req)
                    .then(getData),
            open: (req: PostOpenCombatRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCombatsPostOpenCombat(req)
                    .then(getData),
            endTurn: (req: PostEndTurnRequest) =>
                client.api
                    .takeInitiativeApiFeaturesCombatsPostEndTurn(req)
                    .then(getData),
            stage: {
                character: {
                    upsert: (req: PutUpsertStagedCharacterRequest) =>
                        client.api
                            .takeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter(
                                req,
                            )
                            .then(getData),
                    delete: (req: DeleteStagedCharacterRequest) =>
                        client.api
                            .takeInitiativeApiFeaturesCombatsDeleteStagedCharacter(
                                req,
                            )
                            .then(getData),
                },
                planned: (req: PutStagePlannedCharactersRequest) =>
                    client.api
                        .takeInitiativeApiFeaturesCombatsPostStagePlannedCharacters(
                            req,
                        )
                        .then(getData),
                rollIntoInitiative: (
                    req: PostRollStagedCharactersIntoInitiativeRequest,
                ) =>
                    client.api
                        .takeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative(
                            req,
                        )
                        .then(getData),
                playerCharacters: (req: PostStagePlayerCharactersRequest) =>
                    client.api
                        .takeInitiativeApiFeaturesCombatsPostStagePlayerCharacters(
                            req,
                        )
                        .then(getData),
            },
            initiative: {
                character: {
                    update: (req: PutUpdateInitiativeCharacterRequest) =>
                        client.api
                            .takeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter(
                                req,
                            )
                            .then(getData),
                    delete: (req: DeleteInitiativeCharacterRequest) =>
                        client.api
                            .takeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter(
                                req,
                            )
                            .then(getData),
                },
            },
        },
        admin: {
            getMaintenance: () =>
                client.api
                    .takeInitiativeApiFeaturesAdminGetMaintenanceConfig()
                    .then(getData),
        },
    };
};
