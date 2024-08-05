import type {
    OpenAPIClient,
    Parameters,
    UnknownParamsObject,
    OperationResponse,
    AxiosRequestConfig,
} from "openapi-client-axios";

declare namespace Components {
    namespace Schemas {
        export interface Campaign {
            id?: string; // uuid
            ownerId?: string; // uuid
            campaignName?: string | null;
            campaignDescription?: string | null;
            plannedCombatIds?: string /* uuid */[] | null;
            campaignMemberInfo?: CampaignMemberInfo[] | null;
            activeCombatId?: string | null; // uuid
            createdTimestamp?: string; // date-time
            campaignSettings?: CampaignSettings;
        }
        export interface CampaignMember {
            id?: string; // uuid
            userId?: string; // uuid
            campaignId?: string; // uuid
            isDungeonMaster?: boolean;
            characters?: PlayerCharacter[] | null;
            resources?: CampaignMemberResource[] | null;
        }
        export interface CampaignMemberDto {
            userId?: string; // uuid
            username?: string | null;
            isDungeonMaster?: boolean;
            resources?: CampaignMemberResource[] | null;
        }
        export interface CampaignMemberInfo {
            memberId?: string; // uuid
            userId?: string; // uuid
            isDungeonMaster?: boolean;
        }
        export interface CampaignMemberResource {
            name?: string | null;
            link?: string | null;
            visibility?: 0 | 1 | 2; // int32
        }
        export interface CampaignSettings {
            combatHealthDisplaySettings?: CombatHealthDisplaySettings;
            combatArmourClassDisplaySettings?: CombatArmourClassDisplaySettings;
        }
        export interface CharacterHealth {
            hasHealth?: boolean;
            maxHealth?: number | null; // int32
            currentHealth?: number | null; // int32
        }
        export interface CharacterInitiative {
            strategy?: 0 | 1; // int32
            value?: string | null;
            fixed?: number | null; // int32
            roll?: string | null;
        }
        export interface CharacterOriginDetails {
            characterOrigin?: 0 | 1 | 2; // int32
            id?: string | null; // uuid
        }
        export interface Combat {
            id?: string; // uuid
            campaignId?: string; // uuid
            state?: 0 | 1 | 2 | 3; // int32
            combatName?: string | null;
            dungeonMaster?: string; // uuid
            combatLogs?: string[] | null;
            currentPlayers?: PlayerDto[] | null;
            plannedStages?: PlannedCombatStage[] | null;
            stagedList?: CombatCharacter[] | null;
            initiativeList?: CombatCharacter[] | null;
            initiativeIndex?: number; // int32
            roundNumber?: number | null; // int32
            finishedTimestamp?: string | null; // date-time
        }
        export interface CombatArmourClassDisplaySettings {
            dmCharacterDisplayMethod?: 0 | 2; // int32
            otherPlayerCharacterDisplayMethod?: 0 | 2; // int32
        }
        export interface CombatCharacter {
            id?: string; // uuid
            name?: string | null;
            health?: CharacterHealth;
            initiative?: CharacterInitiative;
            armourClass?: number | null; // int32
            playerId?: string; // uuid
            characterOriginDetails?: CharacterOriginDetails;
            initiativeValue?: number /* int32 */[] | null;
            hidden?: boolean;
            copyNumber?: number | null; // int32
        }
        export interface CombatCharacterDto {
            id?: string; // uuid
            name?: string | null;
            health?: CharacterHealth;
            hidden?: boolean;
            initiativeValue?: number /* int32 */[] | null;
            armourClass?: number | null; // int32
        }
        export interface CombatDto {
            combatId?: string; // uuid
            combatName?: string | null;
            state?: 0 | 1 | 2 | 3; // int32
            finishedTimestamp?: string | null; // date-time
        }
        export interface CombatHealthDisplaySettings {
            dmCharacterDisplayMethod?: 0 | 1 | 2; // int32
            otherPlayerCharacterDisplayMethod?: 0 | 1 | 2; // int32
        }
        export interface CombatHistoryDto {
            lastCombatTimestamp?: string | null; // date-time
            totalCombats?: number; // int32
        }
        export interface CombatResponse {
            combat?: Combat;
        }
        export interface CurrentCombatDto {
            id?: string; // uuid
            state?: 0 | 1 | 2 | 3; // int32
            combatName?: string | null;
            dungeonMaster?: string; // uuid
            currentPlayers?: PlayerDto[] | null;
        }
        export interface DeleteCampaignRequest {
            campaignId?: string; // uuid
        }
        export interface DeleteInitiativeCharacterRequest {
            combatId?: string; // uuid
            characterId?: string; // uuid
        }
        export interface DeletePlannedCombatNpcRequest {
            combatId?: string; // uuid
            stageId?: string; // uuid
            npcId?: string; // uuid
        }
        export interface DeletePlannedCombatRequest {
            campaignId?: string; // uuid
            combatId?: string; // uuid
        }
        export interface DeletePlannedCombatStageRequest {
            combatId?: string; // uuid
            stageId?: string; // uuid
        }
        export interface DeletePlayerCharacterRequest {
            memberId?: string; // uuid
            playerCharacterId?: string; // uuid
        }
        export interface DeleteStagedCharacterRequest {
            characterId?: string; // uuid
            combatId?: string; // uuid
        }
        export interface ErrorResponse {
            statusCode?: number; // int32
            message?: string | null;
            errors?: {
                [name: string]: string[];
            } | null;
        }
        export interface GetCampaignMemberRequest {
            campaignMemberId?: string; // uuid
        }
        export interface GetCampaignRequest {
            campaignId?: string; // uuid
        }
        export interface GetCampaignResponse {
            campaign?: Campaign;
            userCampaignMember?: CampaignMember;
            nonUserCampaignMembers?: CampaignMemberDto[] | null;
            joinCode?: string | null;
            combatHistoryInfo?: CombatHistoryDto;
            currentCombatInfo?: CurrentCombatDto;
        }
        export interface GetCombatHistoryResponse {
            events?: HistoryEvent[] | null;
            playerList?: string /* uuid */[] | null;
        }
        export interface GetCombatRequest {
            id?: string; // uuid
        }
        export interface GetCombatsResponse {
            plannedCombats?: PlannedCombat[] | null;
            combats?: CombatDto[] | null;
        }
        export interface GetUserCampaignDto {
            campaignName?: string | null;
            campaignId?: string; // uuid
            joinCode?: string | null;
        }
        export interface GetUserResponse {
            userId?: string; // uuid
            username?: string | null;
            confirmedEmail?: boolean;
            dmCampaigns?: GetUserCampaignDto[] | null;
            memberCampaigns?: GetUserCampaignDto[] | null;
        }
        export interface HistoryEvent {
            eventName?: string | null;
            userId?: string; // uuid
        }
        export interface JoinCampaignByJoinCodeRequest {
            joinCode?: string | null;
        }
        export interface MaintenanceConfig {
            id?: string; // uuid
            inMaintenanceMode?: boolean;
            reason?: string | null;
        }
        export interface PlannedCombat {
            id?: string; // uuid
            campaignId?: string; // uuid
            combatName?: string | null;
            stages?: PlannedCombatStage[] | null;
        }
        export interface PlannedCombatCharacter {
            id?: string; // uuid
            name?: string | null;
            health?: CharacterHealth;
            initiative?: CharacterInitiative;
            armourClass?: number | null; // int32
            stageId?: string; // uuid
            quantity?: number; // int32
        }
        export interface PlannedCombatStage {
            id?: string; // uuid
            name?: string | null;
            npcs?: PlannedCombatCharacter[] | null;
        }
        export interface PlayerCharacter {
            id?: string; // uuid
            name?: string | null;
            health?: CharacterHealth;
            initiative?: CharacterInitiative;
            armourClass?: number | null; // int32
            playerId?: string; // uuid
        }
        export interface PlayerCharacterDTO {
            name?: string | null;
            health?: CharacterHealth;
            initiative?: CharacterInitiative;
            armourClass?: number | null; // int32
        }
        export interface PlayerDto {
            userId?: string; // uuid
        }
        export interface PostConfirmEmailRequest {
            confirmEmailToken?: string | null;
        }
        export interface PostCreateCampaignRequest {
            campaignName?: string | null;
        }
        export interface PostEndTurnRequest {
            combatId?: string; // uuid
        }
        export interface PostFinishCombatRequest {
            combatId?: string; // uuid
        }
        export interface PostOpenCombatRequest {
            plannedCombatId?: string; // uuid
        }
        export interface PostPlannedCombatNpcRequest {
            combatId?: string; // uuid
            stageId?: string; // uuid
            name?: string | null;
            health?: CharacterHealth;
            armourClass?: number | null; // int32
            initiative?: CharacterInitiative;
            quantity?: number; // int32
        }
        export interface PostPlannedCombatRequest {
            campaignId?: string; // uuid
            combatName?: string | null;
        }
        export interface PostPlannedCombatStageRequest {
            combatId?: string; // uuid
            name?: string | null;
        }
        export interface PostPlayerCharacterRequest {
            campaignMemberId?: string; // uuid
            playerCharacter?: PlayerCharacterDTO;
        }
        export interface PostRollStagedCharactersIntoInitiativeRequest {
            combatId?: string; // uuid
            characterIds?: string /* uuid */[] | null;
        }
        export interface PostSignUpRequest {
            username?: string | null;
            email?: string | null;
            password?: string | null;
        }
        export interface PostStagePlayerCharactersRequest {
            combatId?: string; // uuid
            characterIds?: string /* uuid */[] | null;
        }
        export interface PostStartCombatRequest {
            combatId?: string; // uuid
        }
        export interface PutCampaignDetailsRequest {
            campaignId?: string; // uuid
            campaignName?: string | null;
            campaignDescription?: string | null;
            campaignSettings?: CampaignSettings;
        }
        export interface PutCampaignMemberResourcesRequest {
            memberId?: string; // uuid
            resources?: CampaignMemberResource[] | null;
        }
        export interface PutLoginRequest {
            email?: string | null;
            password?: string | null;
        }
        export interface PutPlannedCombatNpcRequest {
            combatId?: string; // uuid
            stageId?: string; // uuid
            npcId?: string; // uuid
            name?: string | null;
            health?: CharacterHealth;
            armourClass?: number | null; // int32
            initiative?: CharacterInitiative;
            quantity?: number; // int32
        }
        export interface PutPlannedCombatStageRequest {
            combatId?: string; // uuid
            stageId?: string; // uuid
            name?: string | null;
        }
        export interface PutPlayerCharacterRequest {
            campaignMemberId?: string; // uuid
            playerCharacterId?: string; // uuid
            playerCharacter?: PlayerCharacterDTO;
        }
        export interface PutResetPasswordRequest {
            email?: string | null;
            password?: string | null;
            token?: string | null;
        }
        export interface PutSendResetPasswordEmailRequest {
            email?: string | null;
        }
        export interface PutStagePlannedCharactersRequest {
            combatId?: string; // uuid
            plannedCharactersToStage?: {
                [name: string]: StagePlannedCharacterDto[];
            } | null;
        }
        export interface PutUpdateInitiativeCharacterRequest {
            combatId?: string; // uuid
            character?: CombatCharacterDto;
        }
        export interface PutUpsertStagedCharacterRequest {
            combatId?: string; // uuid
            character?: StagedCombatCharacterDto;
        }
        export interface StagePlannedCharacterDto {
            characterId?: string; // uuid
            quantity?: number; // int32
        }
        export interface StagedCombatCharacterDto {
            id?: string; // uuid
            name?: string | null;
            health?: CharacterHealth;
            initiative?: CharacterInitiative;
            armourClass?: number | null; // int32
            hidden?: boolean;
        }
    }
}
declare namespace Paths {
    namespace TakeInitiativeApiFeaturesAdminGetMaintenanceConfig {
        namespace Responses {
            export type $200 = Components.Schemas.MaintenanceConfig;
        }
    }
    namespace TakeInitiativeApiFeaturesAdminPutMaintenanceConfig {
        export type RequestBody = Components.Schemas.MaintenanceConfig;
        namespace Responses {
            export interface $200 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCampaignsDeleteCampaign {
        export type RequestBody = Components.Schemas.DeleteCampaignRequest;
        namespace Responses {
            export interface $200 {}
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCampaignsDeletePlayerCharacter {
        export type RequestBody =
            Components.Schemas.DeletePlayerCharacterRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CampaignMember;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCampaignsGetCampaign {
        export type RequestBody = Components.Schemas.GetCampaignRequest;
        namespace Responses {
            export type $200 = Components.Schemas.GetCampaignResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCampaignsGetCampaignMember {
        export type RequestBody = Components.Schemas.GetCampaignMemberRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CampaignMember;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCampaignsPostCreateCampaign {
        export type RequestBody = Components.Schemas.PostCreateCampaignRequest;
        namespace Responses {
            export type $200 = Components.Schemas.Campaign;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCampaignsPostJoinCampaign {
        export type RequestBody =
            Components.Schemas.JoinCampaignByJoinCodeRequest;
        namespace Responses {
            export type $200 = Components.Schemas.Campaign;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCampaignsPostPlayerCharacter {
        export type RequestBody = Components.Schemas.PostPlayerCharacterRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CampaignMember;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCampaignsPutCampaignDetails {
        export type RequestBody = Components.Schemas.PutCampaignDetailsRequest;
        namespace Responses {
            export type $200 = Components.Schemas.Campaign;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCampaignsPutCampaignMemberResources {
        export type RequestBody =
            Components.Schemas.PutCampaignMemberResourcesRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CampaignMember;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCampaignsPutPlayerCharacter {
        export type RequestBody = Components.Schemas.PutPlayerCharacterRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CampaignMember;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter {
        export type RequestBody =
            Components.Schemas.DeleteInitiativeCharacterRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsDeletePlannedCombat {
        export type RequestBody = Components.Schemas.DeletePlannedCombatRequest;
        namespace Responses {
            export interface $200 {}
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc {
        export type RequestBody =
            Components.Schemas.DeletePlannedCombatNpcRequest;
        namespace Responses {
            export type $200 = Components.Schemas.PlannedCombat;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsDeletePlannedCombatStage {
        export type RequestBody =
            Components.Schemas.DeletePlannedCombatStageRequest;
        namespace Responses {
            export type $200 = Components.Schemas.PlannedCombat;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsDeleteStagedCharacter {
        export type RequestBody =
            Components.Schemas.DeleteStagedCharacterRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsGetCombat {
        export type RequestBody = Components.Schemas.GetCombatRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsGetCombatHistory {
        export type RequestBody = Components.Schemas.GetCombatRequest;
        namespace Responses {
            export type $200 = Components.Schemas.GetCombatHistoryResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsGetCombats {
        namespace Responses {
            export type $200 = Components.Schemas.GetCombatsResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPostEndTurn {
        export type RequestBody = Components.Schemas.PostEndTurnRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPostFinishCombat {
        export type RequestBody = Components.Schemas.PostFinishCombatRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPostOpenCombat {
        export type RequestBody = Components.Schemas.PostOpenCombatRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPostPlannedCombat {
        export type RequestBody = Components.Schemas.PostPlannedCombatRequest;
        namespace Responses {
            export type $200 = Components.Schemas.PlannedCombat;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpc {
        export type RequestBody =
            Components.Schemas.PostPlannedCombatNpcRequest;
        namespace Responses {
            export type $200 = Components.Schemas.PlannedCombat;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPostPlannedCombatStage {
        export type RequestBody =
            Components.Schemas.PostPlannedCombatStageRequest;
        namespace Responses {
            export type $200 = Components.Schemas.PlannedCombat;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative {
        export type RequestBody =
            Components.Schemas.PostRollStagedCharactersIntoInitiativeRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPostStagePlannedCharacters {
        export type RequestBody =
            Components.Schemas.PutStagePlannedCharactersRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPostStagePlayerCharacters {
        export type RequestBody =
            Components.Schemas.PostStagePlayerCharactersRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPostStartCombat {
        export type RequestBody = Components.Schemas.PostStartCombatRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPutPlannedCombatNpc {
        export type RequestBody = Components.Schemas.PutPlannedCombatNpcRequest;
        namespace Responses {
            export type $200 = Components.Schemas.PlannedCombat;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPutPlannedCombatStage {
        export type RequestBody =
            Components.Schemas.PutPlannedCombatStageRequest;
        namespace Responses {
            export type $200 = Components.Schemas.PlannedCombat;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPutReadAndSaveCampaigns {
        namespace Responses {
            export type $200 = any;
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPutReprojectCombats {
        namespace Responses {
            export type $200 = any;
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter {
        export type RequestBody =
            Components.Schemas.PutUpdateInitiativeCharacterRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter {
        export type RequestBody =
            Components.Schemas.PutUpsertStagedCharacterRequest;
        namespace Responses {
            export type $200 = Components.Schemas.CombatResponse;
            export type $400 = Components.Schemas.ErrorResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesUsersGetUser {
        namespace Responses {
            export type $200 = Components.Schemas.GetUserResponse;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesUsersPostConfirmEmail {
        export type RequestBody = Components.Schemas.PostConfirmEmailRequest;
        namespace Responses {
            export type $200 = Components.Schemas.GetUserResponse;
            export type $400 = Components.Schemas.ErrorResponse;
        }
    }
    namespace TakeInitiativeApiFeaturesUsersPostLogout {
        namespace Responses {
            export type $200 = any;
            export interface $401 {}
            export interface $403 {}
        }
    }
    namespace TakeInitiativeApiFeaturesUsersPostSendConfirmEmail {
        namespace Responses {
            export type $200 = any;
        }
    }
    namespace TakeInitiativeApiFeaturesUsersPostSignUp {
        export type RequestBody = Components.Schemas.PostSignUpRequest;
        namespace Responses {
            export interface $200 {}
            export type $400 = Components.Schemas.ErrorResponse;
        }
    }
    namespace TakeInitiativeApiFeaturesUsersPutLogin {
        export type RequestBody = Components.Schemas.PutLoginRequest;
        namespace Responses {
            export interface $200 {}
        }
    }
    namespace TakeInitiativeApiFeaturesUsersPutResetPassword {
        export type RequestBody = Components.Schemas.PutResetPasswordRequest;
        namespace Responses {
            export interface $200 {}
            export type $400 = Components.Schemas.ErrorResponse;
        }
    }
    namespace TakeInitiativeApiFeaturesUsersPutSendResetPasswordEmail {
        export type RequestBody =
            Components.Schemas.PutSendResetPasswordEmailRequest;
        namespace Responses {
            export interface $200 {}
            export type $400 = Components.Schemas.ErrorResponse;
        }
    }
}

export interface OperationMethods {
    /**
     * TakeInitiativeApiFeaturesUsersGetUser
     */
    "TakeInitiativeApiFeaturesUsersGetUser"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: any,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersGetUser.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesUsersPostConfirmEmail
     */
    "TakeInitiativeApiFeaturesUsersPostConfirmEmail"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesUsersPostConfirmEmail.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPostConfirmEmail.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesUsersPostLogout
     */
    "TakeInitiativeApiFeaturesUsersPostLogout"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: any,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPostLogout.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesUsersPostSendConfirmEmail
     */
    "TakeInitiativeApiFeaturesUsersPostSendConfirmEmail"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: any,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPostSendConfirmEmail.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesUsersPostSignUp
     */
    "TakeInitiativeApiFeaturesUsersPostSignUp"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesUsersPostSignUp.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPostSignUp.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesUsersPutLogin
     */
    "TakeInitiativeApiFeaturesUsersPutLogin"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesUsersPutLogin.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPutLogin.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesUsersPutResetPassword
     */
    "TakeInitiativeApiFeaturesUsersPutResetPassword"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesUsersPutResetPassword.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPutResetPassword.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesUsersPutSendResetPasswordEmail
     */
    "TakeInitiativeApiFeaturesUsersPutSendResetPasswordEmail"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesUsersPutSendResetPasswordEmail.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPutSendResetPasswordEmail.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCampaignsPutPlayerCharacter
     */
    "TakeInitiativeApiFeaturesCampaignsPutPlayerCharacter"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCampaignsPutPlayerCharacter.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPutPlayerCharacter.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCampaignsPostPlayerCharacter
     */
    "TakeInitiativeApiFeaturesCampaignsPostPlayerCharacter"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCampaignsPostPlayerCharacter.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPostPlayerCharacter.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCampaignsDeletePlayerCharacter
     */
    "TakeInitiativeApiFeaturesCampaignsDeletePlayerCharacter"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCampaignsDeletePlayerCharacter.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsDeletePlayerCharacter.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCampaignsGetCampaignMember
     */
    "TakeInitiativeApiFeaturesCampaignsGetCampaignMember"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCampaignsGetCampaignMember.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsGetCampaignMember.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCampaignsPutCampaignMemberResources
     */
    "TakeInitiativeApiFeaturesCampaignsPutCampaignMemberResources"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCampaignsPutCampaignMemberResources.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPutCampaignMemberResources.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCampaignsPutCampaignDetails
     */
    "TakeInitiativeApiFeaturesCampaignsPutCampaignDetails"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCampaignsPutCampaignDetails.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPutCampaignDetails.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCampaignsPostCreateCampaign
     */
    "TakeInitiativeApiFeaturesCampaignsPostCreateCampaign"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCampaignsPostCreateCampaign.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPostCreateCampaign.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCampaignsDeleteCampaign
     */
    "TakeInitiativeApiFeaturesCampaignsDeleteCampaign"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCampaignsDeleteCampaign.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsDeleteCampaign.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCampaignsGetCampaign
     */
    "TakeInitiativeApiFeaturesCampaignsGetCampaign"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCampaignsGetCampaign.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsGetCampaign.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCampaignsPostJoinCampaign
     */
    "TakeInitiativeApiFeaturesCampaignsPostJoinCampaign"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCampaignsPostJoinCampaign.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPostJoinCampaign.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPutReadAndSaveCampaigns
     */
    "TakeInitiativeApiFeaturesCombatsPutReadAndSaveCampaigns"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: any,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutReadAndSaveCampaigns.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPutReprojectCombats
     */
    "TakeInitiativeApiFeaturesCombatsPutReprojectCombats"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: any,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutReprojectCombats.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter
     */
    "TakeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter
     */
    "TakeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter
     */
    "TakeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsDeleteStagedCharacter
     */
    "TakeInitiativeApiFeaturesCombatsDeleteStagedCharacter"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsDeleteStagedCharacter.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsDeleteStagedCharacter.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsGetCombatHistory
     */
    "TakeInitiativeApiFeaturesCombatsGetCombatHistory"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsGetCombatHistory.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsGetCombatHistory.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsGetCombats
     */
    "TakeInitiativeApiFeaturesCombatsGetCombats"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: any,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsGetCombats.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsGetCombat
     */
    "TakeInitiativeApiFeaturesCombatsGetCombat"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsGetCombat.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsGetCombat.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPostPlannedCombat
     */
    "TakeInitiativeApiFeaturesCombatsPostPlannedCombat"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombat.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombat.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsDeletePlannedCombat
     */
    "TakeInitiativeApiFeaturesCombatsDeletePlannedCombat"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombat.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombat.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPutPlannedCombatStage
     */
    "TakeInitiativeApiFeaturesCombatsPutPlannedCombatStage"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPutPlannedCombatStage.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutPlannedCombatStage.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPostPlannedCombatStage
     */
    "TakeInitiativeApiFeaturesCombatsPostPlannedCombatStage"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombatStage.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombatStage.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsDeletePlannedCombatStage
     */
    "TakeInitiativeApiFeaturesCombatsDeletePlannedCombatStage"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombatStage.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombatStage.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPutPlannedCombatNpc
     */
    "TakeInitiativeApiFeaturesCombatsPutPlannedCombatNpc"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPutPlannedCombatNpc.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutPlannedCombatNpc.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpc
     */
    "TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpc"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpc.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpc.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc
     */
    "TakeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPostEndTurn
     */
    "TakeInitiativeApiFeaturesCombatsPostEndTurn"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPostEndTurn.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostEndTurn.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPostFinishCombat
     */
    "TakeInitiativeApiFeaturesCombatsPostFinishCombat"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPostFinishCombat.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostFinishCombat.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPostOpenCombat
     */
    "TakeInitiativeApiFeaturesCombatsPostOpenCombat"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPostOpenCombat.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostOpenCombat.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative
     */
    "TakeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPostStagePlannedCharacters
     */
    "TakeInitiativeApiFeaturesCombatsPostStagePlannedCharacters"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPostStagePlannedCharacters.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostStagePlannedCharacters.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPostStagePlayerCharacters
     */
    "TakeInitiativeApiFeaturesCombatsPostStagePlayerCharacters"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPostStagePlayerCharacters.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostStagePlayerCharacters.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesCombatsPostStartCombat
     */
    "TakeInitiativeApiFeaturesCombatsPostStartCombat"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesCombatsPostStartCombat.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostStartCombat.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesAdminGetMaintenanceConfig
     */
    "TakeInitiativeApiFeaturesAdminGetMaintenanceConfig"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: any,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesAdminGetMaintenanceConfig.Responses.$200>;
    /**
     * TakeInitiativeApiFeaturesAdminPutMaintenanceConfig
     */
    "TakeInitiativeApiFeaturesAdminPutMaintenanceConfig"(
        parameters?: Parameters<UnknownParamsObject> | null,
        data?: Paths.TakeInitiativeApiFeaturesAdminPutMaintenanceConfig.RequestBody,
        config?: AxiosRequestConfig,
    ): OperationResponse<Paths.TakeInitiativeApiFeaturesAdminPutMaintenanceConfig.Responses.$200>;
}

export interface PathsDictionary {
    ["/api/user"]: {
        /**
         * TakeInitiativeApiFeaturesUsersGetUser
         */
        "get"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: any,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersGetUser.Responses.$200>;
    };
    ["/api/confirmEmail"]: {
        /**
         * TakeInitiativeApiFeaturesUsersPostConfirmEmail
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesUsersPostConfirmEmail.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPostConfirmEmail.Responses.$200>;
    };
    ["/api/logout"]: {
        /**
         * TakeInitiativeApiFeaturesUsersPostLogout
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: any,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPostLogout.Responses.$200>;
    };
    ["/api/sendConfirmEmail"]: {
        /**
         * TakeInitiativeApiFeaturesUsersPostSendConfirmEmail
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: any,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPostSendConfirmEmail.Responses.$200>;
    };
    ["/api/signup"]: {
        /**
         * TakeInitiativeApiFeaturesUsersPostSignUp
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesUsersPostSignUp.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPostSignUp.Responses.$200>;
    };
    ["/api/login"]: {
        /**
         * TakeInitiativeApiFeaturesUsersPutLogin
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesUsersPutLogin.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPutLogin.Responses.$200>;
    };
    ["/api/resetPassword"]: {
        /**
         * TakeInitiativeApiFeaturesUsersPutResetPassword
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesUsersPutResetPassword.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPutResetPassword.Responses.$200>;
    };
    ["/api/sendResetPasswordEmail"]: {
        /**
         * TakeInitiativeApiFeaturesUsersPutSendResetPasswordEmail
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesUsersPutSendResetPasswordEmail.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesUsersPutSendResetPasswordEmail.Responses.$200>;
    };
    ["/api/campaign/member/character"]: {
        /**
         * TakeInitiativeApiFeaturesCampaignsDeletePlayerCharacter
         */
        "delete"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCampaignsDeletePlayerCharacter.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsDeletePlayerCharacter.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCampaignsPostPlayerCharacter
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCampaignsPostPlayerCharacter.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPostPlayerCharacter.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCampaignsPutPlayerCharacter
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCampaignsPutPlayerCharacter.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPutPlayerCharacter.Responses.$200>;
    };
    ["/api/campaign/member"]: {
        /**
         * TakeInitiativeApiFeaturesCampaignsGetCampaignMember
         */
        "get"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCampaignsGetCampaignMember.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsGetCampaignMember.Responses.$200>;
    };
    ["/api/campaign/member/resources"]: {
        /**
         * TakeInitiativeApiFeaturesCampaignsPutCampaignMemberResources
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCampaignsPutCampaignMemberResources.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPutCampaignMemberResources.Responses.$200>;
    };
    ["/api/campaign"]: {
        /**
         * TakeInitiativeApiFeaturesCampaignsDeleteCampaign
         */
        "delete"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCampaignsDeleteCampaign.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsDeleteCampaign.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCampaignsPostCreateCampaign
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCampaignsPostCreateCampaign.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPostCreateCampaign.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCampaignsPutCampaignDetails
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCampaignsPutCampaignDetails.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPutCampaignDetails.Responses.$200>;
    };
    ["/api/campaign/{CampaignId}"]: {
        /**
         * TakeInitiativeApiFeaturesCampaignsGetCampaign
         */
        "get"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCampaignsGetCampaign.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsGetCampaign.Responses.$200>;
    };
    ["/api/campaign/join"]: {
        /**
         * TakeInitiativeApiFeaturesCampaignsPostJoinCampaign
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCampaignsPostJoinCampaign.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCampaignsPostJoinCampaign.Responses.$200>;
    };
    ["/api/admin/readAndSave/campaigns"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsPutReadAndSaveCampaigns
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: any,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutReadAndSaveCampaigns.Responses.$200>;
    };
    ["/api/admin/reproject/combat"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsPutReprojectCombats
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: any,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutReprojectCombats.Responses.$200>;
    };
    ["/api/combat/initiative/character"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter
         */
        "delete"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter.Responses.$200>;
    };
    ["/api/combat/stage/character"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsDeleteStagedCharacter
         */
        "delete"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsDeleteStagedCharacter.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsDeleteStagedCharacter.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter.Responses.$200>;
    };
    ["/api/combat/{id}/history"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsGetCombatHistory
         */
        "get"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsGetCombatHistory.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsGetCombatHistory.Responses.$200>;
    };
    ["/api/combats"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsGetCombats
         */
        "get"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: any,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsGetCombats.Responses.$200>;
    };
    ["/api/combat/{Id}"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsGetCombat
         */
        "get"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsGetCombat.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsGetCombat.Responses.$200>;
    };
    ["/api/combat/planned"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsDeletePlannedCombat
         */
        "delete"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombat.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombat.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCombatsPostPlannedCombat
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombat.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombat.Responses.$200>;
    };
    ["/api/campaign/planned-combat/stage"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsDeletePlannedCombatStage
         */
        "delete"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombatStage.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombatStage.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCombatsPostPlannedCombatStage
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombatStage.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombatStage.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCombatsPutPlannedCombatStage
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPutPlannedCombatStage.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutPlannedCombatStage.Responses.$200>;
    };
    ["/api/campaign/planned-combat/stage/npc"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc
         */
        "delete"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpc
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpc.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpc.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesCombatsPutPlannedCombatNpc
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPutPlannedCombatNpc.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPutPlannedCombatNpc.Responses.$200>;
    };
    ["/api/combat/turn/end"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsPostEndTurn
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPostEndTurn.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostEndTurn.Responses.$200>;
    };
    ["/api/combat/finish"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsPostFinishCombat
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPostFinishCombat.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostFinishCombat.Responses.$200>;
    };
    ["/api/combat/open"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsPostOpenCombat
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPostOpenCombat.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostOpenCombat.Responses.$200>;
    };
    ["/api/combat/stage/roll"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative.Responses.$200>;
    };
    ["/api/combat/stage/planned-character"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsPostStagePlannedCharacters
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPostStagePlannedCharacters.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostStagePlannedCharacters.Responses.$200>;
    };
    ["/api/combat/stage/player-character"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsPostStagePlayerCharacters
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPostStagePlayerCharacters.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostStagePlayerCharacters.Responses.$200>;
    };
    ["/api/combat/start"]: {
        /**
         * TakeInitiativeApiFeaturesCombatsPostStartCombat
         */
        "post"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesCombatsPostStartCombat.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesCombatsPostStartCombat.Responses.$200>;
    };
    ["/api/admin/maintenance"]: {
        /**
         * TakeInitiativeApiFeaturesAdminGetMaintenanceConfig
         */
        "get"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: any,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesAdminGetMaintenanceConfig.Responses.$200>;
        /**
         * TakeInitiativeApiFeaturesAdminPutMaintenanceConfig
         */
        "put"(
            parameters?: Parameters<UnknownParamsObject> | null,
            data?: Paths.TakeInitiativeApiFeaturesAdminPutMaintenanceConfig.RequestBody,
            config?: AxiosRequestConfig,
        ): OperationResponse<Paths.TakeInitiativeApiFeaturesAdminPutMaintenanceConfig.Responses.$200>;
    };
}

export type Client = OpenAPIClient<OperationMethods, PathsDictionary>;
