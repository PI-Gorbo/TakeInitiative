/* eslint-disable */
/* tslint:disable */
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

export interface Campaign {
    /** @format uuid */
    id?: string;
    /** @format uuid */
    ownerId?: string;
    campaignName?: string | null;
    campaignDescription?: string | null;
    plannedCombatIds?: string[] | null;
    campaignMemberInfo?: CampaignMemberInfo[] | null;
    /** @format uuid */
    activeCombatId?: string | null;
    /** @format date-time */
    createdTimestamp?: string;
    campaignSettings?: CampaignSettings;
}

export interface CampaignMember {
    /** @format uuid */
    id?: string;
    /** @format uuid */
    userId?: string;
    /** @format uuid */
    campaignId?: string;
    isDungeonMaster?: boolean;
    characters?: PlayerCharacter[] | null;
    resources?: CampaignMemberResource[] | null;
}

export interface CampaignMemberDto {
    /** @format uuid */
    userId?: string;
    username?: string | null;
    isDungeonMaster?: boolean;
    resources?: CampaignMemberResource[] | null;
}

export interface CampaignMemberInfo {
    /** @format uuid */
    memberId?: string;
    /** @format uuid */
    userId?: string;
    isDungeonMaster?: boolean;
}

export interface CampaignMemberResource {
    name?: string | null;
    link?: string | null;
    visibility?: ResourceVisibilityOptions;
}

export interface CampaignSettings {
    combatHealthDisplaySettings?: CombatHealthDisplaySettings;
    combatArmourClassDisplaySettings?: CombatArmourClassDisplaySettings;
}

export interface Character {
    /** @format uuid */
    id?: string;
    name?: string | null;
    health?: CharacterHealth;
    initiative?: CharacterInitiative;
    /** @format int32 */
    armourClass?: number | null;
}

export interface CharacterHealth {
    hasHealth?: boolean;
    /** @format int32 */
    maxHealth?: number | null;
    /** @format int32 */
    currentHealth?: number | null;
}

export interface CharacterInitiative {
    strategy?: InitiativeStrategy;
    value?: string | null;
    /** @format int32 */
    fixed?: number | null;
    roll?: string | null;
}

export interface CharacterOriginDetails {
    characterOrigin?: CharacterOriginOptions;
    /** @format uuid */
    id?: string | null;
}

/** @format int32 */
export enum CharacterOriginOptions {
    Value0 = 0,
    Value1 = 1,
    Value2 = 2,
}

export interface Combat {
    /** @format uuid */
    id?: string;
    /** @format uuid */
    campaignId?: string;
    state?: CombatState;
    combatName?: string | null;
    /** @format uuid */
    dungeonMaster?: string;
    history?: CombatLog[] | null;
    currentPlayers?: PlayerDto[] | null;
    plannedStages?: PlannedCombatStage[] | null;
    stagedList?: CombatCharacter[] | null;
    initiativeList?: CombatCharacter[] | null;
    /** @format int32 */
    initiativeIndex?: number;
    /** @format int32 */
    roundNumber?: number | null;
    /** @format date-time */
    finishedTimestamp?: string | null;
}

export interface CombatArmourClassDisplaySettings {
    dmCharacterDisplayMethod?: CombatArmourDisplayOptions;
    otherPlayerCharacterDisplayMethod?: CombatArmourDisplayOptions;
}

/** @format int32 */
export enum CombatArmourDisplayOptions {
    Value0 = 0,
    Value2 = 2,
}

export type CombatCharacter = Character & {
    /** @format uuid */
    playerId?: string;
    characterOriginDetails?: CharacterOriginDetails;
    initiativeValue?: number[] | null;
    hidden?: boolean;
    /** @format int32 */
    copyNumber?: number | null;
};

export interface CombatCharacterDto {
    /** @format uuid */
    id?: string;
    name?: string | null;
    health?: CharacterHealth;
    hidden?: boolean;
    initiativeValue?: number[] | null;
    /** @format int32 */
    armourClass?: number | null;
}

export interface CombatDto {
    /** @format uuid */
    combatId?: string;
    combatName?: string | null;
    state?: CombatState;
    /** @format date-time */
    finishedTimestamp?: string | null;
}

/** @format int32 */
export enum CombatHealthDisplayOptions {
    Value0 = 0,
    Value1 = 1,
    Value2 = 2,
}

export interface CombatHealthDisplaySettings {
    dmCharacterDisplayMethod?: CombatHealthDisplayOptions;
    otherPlayerCharacterDisplayMethod?: CombatHealthDisplayOptions;
}

export interface CombatHistoryDto {
    /** @format date-time */
    lastCombatTimestamp?: string | null;
    /** @format int32 */
    totalCombats?: number;
}

export interface CombatLog {
    operations?: ICombatOperation[] | null;
    /** @format uuid */
    operatorId?: string;
    /** @format date-time */
    time?: string;
}

export interface CombatResponse {
    combat?: Combat;
}

/** @format int32 */
export enum CombatState {
    Value0 = 0,
    Value1 = 1,
    Value2 = 2,
    Value3 = 3,
}

export interface CurrentCombatDto {
    /** @format uuid */
    id?: string;
    state?: CombatState;
    combatName?: string | null;
    /** @format uuid */
    dungeonMaster?: string;
    currentPlayers?: PlayerDto[] | null;
}

export interface DeleteCampaignRequest {
    /** @format uuid */
    campaignId?: string;
}

export interface DeleteInitiativeCharacterRequest {
    /** @format uuid */
    combatId?: string;
    /** @format uuid */
    characterId?: string;
}

export interface DeletePlannedCombatNpcRequest {
    /** @format uuid */
    combatId?: string;
    /** @format uuid */
    stageId?: string;
    /** @format uuid */
    npcId?: string;
}

export interface DeletePlannedCombatRequest {
    /** @format uuid */
    campaignId?: string;
    /** @format uuid */
    combatId?: string;
}

export interface DeletePlannedCombatStageRequest {
    /** @format uuid */
    combatId?: string;
    /** @format uuid */
    stageId?: string;
}

export interface DeletePlayerCharacterRequest {
    /** @format uuid */
    memberId?: string;
    /** @format uuid */
    playerCharacterId?: string;
}

export interface DeleteStagedCharacterRequest {
    /** @format uuid */
    characterId?: string;
    /** @format uuid */
    combatId?: string;
}

export interface ErrorResponse {
    /**
     * @format int32
     * @default 400
     */
    statusCode?: number;
    /** @default "One or more errors occurred!" */
    message?: string | null;
    errors?: Record<string, string[]>;
}

export interface GetCampaignMemberRequest {
    /** @format uuid */
    campaignMemberId?: string;
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
    playerList?: string[] | null;
}

export interface GetCombatRequest {
    /** @format uuid */
    id?: string;
}

export interface GetCombatsRequest {
    /** @format uuid */
    campaignId?: string;
}

export interface GetCombatsResponse {
    plannedCombats?: PlannedCombat[] | null;
    combats?: CombatDto[] | null;
}

export interface GetUserCampaignDto {
    campaignName?: string | null;
    /** @format uuid */
    campaignId?: string;
    joinCode?: string | null;
}

export interface GetUserResponse {
    /** @format uuid */
    userId?: string;
    username?: string | null;
    confirmedEmail?: boolean;
    dmCampaigns?: GetUserCampaignDto[] | null;
    memberCampaigns?: GetUserCampaignDto[] | null;
}

export interface HistoryEvent {
    eventName?: string | null;
    /** @format uuid */
    userId?: string;
}

export type ICombatOperation = object;

/** @format int32 */
export enum InitiativeStrategy {
    Value0 = 0,
    Value1 = 1,
}

export interface JoinCampaignByJoinCodeRequest {
    joinCode?: string | null;
}

export interface MaintenanceConfig {
    /** @format uuid */
    id?: string;
    inMaintenanceMode?: boolean;
    reason?: string | null;
}

export interface PlannedCombat {
    /** @format uuid */
    id?: string;
    /** @format uuid */
    campaignId?: string;
    combatName?: string | null;
    stages?: PlannedCombatStage[] | null;
}

export type PlannedCombatCharacter = Character & {
    /** @format uuid */
    stageId?: string;
    /** @format int32 */
    quantity?: number;
};

export interface PlannedCombatStage {
    /** @format uuid */
    id?: string;
    name?: string | null;
    npcs?: PlannedCombatCharacter[] | null;
}

export type PlayerCharacter = Character & {
    /** @format uuid */
    playerId?: string;
};

export interface PlayerCharacterDTO {
    name?: string | null;
    health?: CharacterHealth;
    initiative?: CharacterInitiative;
    /** @format int32 */
    armourClass?: number | null;
}

export interface PlayerDto {
    /** @format uuid */
    userId?: string;
}

export interface PostConfirmEmailRequest {
    confirmEmailToken?: string | null;
}

export interface PostCreateCampaignRequest {
    campaignName?: string | null;
}

export interface PostEndTurnRequest {
    /** @format uuid */
    combatId?: string;
}

export interface PostFinishCombatRequest {
    /** @format uuid */
    combatId?: string;
}

export interface PostOpenCombatRequest {
    /** @format uuid */
    plannedCombatId?: string;
}

export interface PostPlannedCombatNpcRequest {
    /** @format uuid */
    combatId?: string;
    /** @format uuid */
    stageId?: string;
    name?: string | null;
    health?: CharacterHealth;
    /** @format int32 */
    armourClass?: number | null;
    initiative?: CharacterInitiative;
    /** @format int32 */
    quantity?: number;
}

export interface PostPlannedCombatRequest {
    /** @format uuid */
    campaignId?: string;
    combatName?: string | null;
}

export interface PostPlannedCombatStageRequest {
    /** @format uuid */
    combatId?: string;
    name?: string | null;
}

export interface PostPlayerCharacterRequest {
    /** @format uuid */
    campaignMemberId?: string;
    playerCharacter?: PlayerCharacterDTO;
}

export interface PostRollStagedCharactersIntoInitiativeRequest {
    /** @format uuid */
    combatId?: string;
    characterIds?: string[] | null;
}

export interface PostSignUpRequest {
    username?: string | null;
    email?: string | null;
    password?: string | null;
}

export interface PostStagePlayerCharactersRequest {
    /** @format uuid */
    combatId?: string;
    characterIds?: string[] | null;
}

export interface PostStartCombatRequest {
    /** @format uuid */
    combatId?: string;
}

export interface PutCampaignDetailsRequest {
    /** @format uuid */
    campaignId?: string;
    campaignName?: string | null;
    campaignDescription?: string | null;
    campaignSettings?: CampaignSettings;
}

export interface PutCampaignMemberResourcesRequest {
    /** @format uuid */
    memberId?: string;
    resources?: CampaignMemberResource[] | null;
}

export interface PutLoginRequest {
    email?: string | null;
    password?: string | null;
}

export interface PutPlannedCombatNpcRequest {
    /** @format uuid */
    combatId?: string;
    /** @format uuid */
    stageId?: string;
    /** @format uuid */
    npcId?: string;
    name?: string | null;
    health?: CharacterHealth;
    /** @format int32 */
    armourClass?: number | null;
    initiative?: CharacterInitiative;
    /** @format int32 */
    quantity?: number;
}

export interface PutPlannedCombatStageRequest {
    /** @format uuid */
    combatId?: string;
    /** @format uuid */
    stageId?: string;
    name?: string | null;
}

export interface PutPlayerCharacterRequest {
    /** @format uuid */
    campaignMemberId?: string;
    /** @format uuid */
    playerCharacterId?: string;
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
    /** @format uuid */
    combatId?: string;
    plannedCharactersToStage?: Record<string, StagePlannedCharacterDto[]>;
}

export interface PutUpdateInitiativeCharacterRequest {
    /** @format uuid */
    combatId?: string;
    character?: CombatCharacterDto;
}

export interface PutUpsertStagedCharacterRequest {
    /** @format uuid */
    combatId?: string;
    character?: StagedCombatCharacterDto;
}

/** @format int32 */
export enum ResourceVisibilityOptions {
    Value0 = 0,
    Value1 = 1,
    Value2 = 2,
}

export interface StagePlannedCharacterDto {
    /** @format uuid */
    characterId?: string;
    /** @format int32 */
    quantity?: number;
}

export type StagedCombatCharacterDto = Character & {
    hidden?: boolean;
};

import type { AxiosInstance, AxiosRequestConfig, AxiosResponse, HeadersDefaults, ResponseType } from "axios";
import axios from "axios";

export type QueryParamsType = Record<string | number, any>;

export interface FullRequestParams extends Omit<AxiosRequestConfig, "data" | "params" | "url" | "responseType"> {
    /** set parameter to `true` for call `securityWorker` for this request */
    secure?: boolean;
    /** request path */
    path: string;
    /** content type of request body */
    type?: ContentType;
    /** query params */
    query?: QueryParamsType;
    /** format of response (i.e. response.json() -> format: "json") */
    format?: ResponseType;
    /** request body */
    body?: unknown;
}

export type RequestParams = Omit<FullRequestParams, "body" | "method" | "query" | "path">;

export interface ApiConfig<SecurityDataType = unknown> extends Omit<AxiosRequestConfig, "data" | "cancelToken"> {
    securityWorker?: (
        securityData: SecurityDataType | null,
    ) => Promise<AxiosRequestConfig | void> | AxiosRequestConfig | void;
    secure?: boolean;
    format?: ResponseType;
}

export enum ContentType {
    Json = "application/json",
    FormData = "multipart/form-data",
    UrlEncoded = "application/x-www-form-urlencoded",
    Text = "text/plain",
}

export class HttpClient<SecurityDataType = unknown> {
    public instance: AxiosInstance;
    private securityData: SecurityDataType | null = null;
    private securityWorker?: ApiConfig<SecurityDataType>["securityWorker"];
    private secure?: boolean;
    private format?: ResponseType;

    constructor({ securityWorker, secure, format, ...axiosConfig }: ApiConfig<SecurityDataType> = {}) {
        this.instance = axios.create({ ...axiosConfig, baseURL: axiosConfig.baseURL || "" });
        this.secure = secure;
        this.format = format;
        this.securityWorker = securityWorker;
    }

    public setSecurityData = (data: SecurityDataType | null) => {
        this.securityData = data;
    };

    protected mergeRequestParams(params1: AxiosRequestConfig, params2?: AxiosRequestConfig): AxiosRequestConfig {
        const method = params1.method || (params2 && params2.method);

        return {
            ...this.instance.defaults,
            ...params1,
            ...(params2 || {}),
            headers: {
                ...((method && this.instance.defaults.headers[method.toLowerCase() as keyof HeadersDefaults]) || {}),
                ...(params1.headers || {}),
                ...((params2 && params2.headers) || {}),
            },
        };
    }

    protected stringifyFormItem(formItem: unknown) {
        if (typeof formItem === "object" && formItem !== null) {
            return JSON.stringify(formItem);
        } else {
            return `${formItem}`;
        }
    }

    protected createFormData(input: Record<string, unknown>): FormData {
        if (input instanceof FormData) {
            return input;
        }
        return Object.keys(input || {}).reduce((formData, key) => {
            const property = input[key];
            const propertyContent: any[] = property instanceof Array ? property : [property];

            for (const formItem of propertyContent) {
                const isFileType = formItem instanceof Blob || formItem instanceof File;
                formData.append(key, isFileType ? formItem : this.stringifyFormItem(formItem));
            }

            return formData;
        }, new FormData());
    }

    public request = async <T = any, _E = any>({
        secure,
        path,
        type,
        query,
        format,
        body,
        ...params
    }: FullRequestParams): Promise<AxiosResponse<T>> => {
        const secureParams =
            ((typeof secure === "boolean" ? secure : this.secure) &&
                this.securityWorker &&
                (await this.securityWorker(this.securityData))) ||
            {};
        const requestParams = this.mergeRequestParams(params, secureParams);
        const responseFormat = format || this.format || undefined;

        if (type === ContentType.FormData && body && body !== null && typeof body === "object") {
            body = this.createFormData(body as Record<string, unknown>);
        }

        if (type === ContentType.Text && body && body !== null && typeof body !== "string") {
            body = JSON.stringify(body);
        }

        return this.instance.request({
            ...requestParams,
            headers: {
                ...(requestParams.headers || {}),
                ...(type ? { "Content-Type": type } : {}),
            },
            params: query,
            responseType: responseFormat,
            data: body,
            url: path,
        });
    };
}

/**
 * @title TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
 * @version 1.0
 */
export class Api<SecurityDataType extends unknown> extends HttpClient<SecurityDataType> {
    api = {
        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesUsersGetUser
         * @request GET:/api/user
         */
        takeInitiativeApiFeaturesUsersGetUser: (params: RequestParams = {}) =>
            this.request<GetUserResponse, void>({
                path: `/api/user`,
                method: "GET",
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesUsersPostConfirmEmail
         * @request POST:/api/confirmEmail
         */
        takeInitiativeApiFeaturesUsersPostConfirmEmail: (data: PostConfirmEmailRequest, params: RequestParams = {}) =>
            this.request<GetUserResponse, ErrorResponse>({
                path: `/api/confirmEmail`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesUsersPostLogout
         * @request POST:/api/logout
         */
        takeInitiativeApiFeaturesUsersPostLogout: (params: RequestParams = {}) =>
            this.request<any, void>({
                path: `/api/logout`,
                method: "POST",
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesUsersPostSendConfirmEmail
         * @request POST:/api/sendConfirmEmail
         */
        takeInitiativeApiFeaturesUsersPostSendConfirmEmail: (params: RequestParams = {}) =>
            this.request<any, any>({
                path: `/api/sendConfirmEmail`,
                method: "POST",
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesUsersPostSignUp
         * @request POST:/api/signup
         */
        takeInitiativeApiFeaturesUsersPostSignUp: (data: PostSignUpRequest, params: RequestParams = {}) =>
            this.request<void, ErrorResponse>({
                path: `/api/signup`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesUsersPutLogin
         * @request PUT:/api/login
         */
        takeInitiativeApiFeaturesUsersPutLogin: (data: PutLoginRequest, params: RequestParams = {}) =>
            this.request<void, any>({
                path: `/api/login`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesUsersPutResetPassword
         * @request PUT:/api/resetPassword
         */
        takeInitiativeApiFeaturesUsersPutResetPassword: (data: PutResetPasswordRequest, params: RequestParams = {}) =>
            this.request<void, ErrorResponse>({
                path: `/api/resetPassword`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesUsersPutSendResetPasswordEmail
         * @request PUT:/api/sendResetPasswordEmail
         */
        takeInitiativeApiFeaturesUsersPutSendResetPasswordEmail: (
            data: PutSendResetPasswordEmailRequest,
            params: RequestParams = {},
        ) =>
            this.request<void, ErrorResponse>({
                path: `/api/sendResetPasswordEmail`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCampaignsDeletePlayerCharacter
         * @request DELETE:/api/campaign/member/character
         */
        takeInitiativeApiFeaturesCampaignsDeletePlayerCharacter: (
            data: DeletePlayerCharacterRequest,
            params: RequestParams = {},
        ) =>
            this.request<CampaignMember, void>({
                path: `/api/campaign/member/character`,
                method: "DELETE",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCampaignsPostPlayerCharacter
         * @request POST:/api/campaign/member/character
         */
        takeInitiativeApiFeaturesCampaignsPostPlayerCharacter: (
            data: PostPlayerCharacterRequest,
            params: RequestParams = {},
        ) =>
            this.request<CampaignMember, ErrorResponse | void>({
                path: `/api/campaign/member/character`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCampaignsPutPlayerCharacter
         * @request PUT:/api/campaign/member/character
         */
        takeInitiativeApiFeaturesCampaignsPutPlayerCharacter: (
            data: PutPlayerCharacterRequest,
            params: RequestParams = {},
        ) =>
            this.request<CampaignMember, ErrorResponse | void>({
                path: `/api/campaign/member/character`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCampaignsGetCampaignMember
         * @request GET:/api/campaign/member
         */
        takeInitiativeApiFeaturesCampaignsGetCampaignMember: (
            data: GetCampaignMemberRequest,
            params: RequestParams = {},
        ) =>
            this.request<CampaignMember, void>({
                path: `/api/campaign/member`,
                method: "GET",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCampaignsPutCampaignMemberResources
         * @request PUT:/api/campaign/member/resources
         */
        takeInitiativeApiFeaturesCampaignsPutCampaignMemberResources: (
            data: PutCampaignMemberResourcesRequest,
            params: RequestParams = {},
        ) =>
            this.request<CampaignMember, void>({
                path: `/api/campaign/member/resources`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCampaignsDeleteCampaign
         * @request DELETE:/api/campaign
         */
        takeInitiativeApiFeaturesCampaignsDeleteCampaign: (data: DeleteCampaignRequest, params: RequestParams = {}) =>
            this.request<void, void>({
                path: `/api/campaign`,
                method: "DELETE",
                body: data,
                type: ContentType.Json,
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCampaignsPostCreateCampaign
         * @request POST:/api/campaign
         */
        takeInitiativeApiFeaturesCampaignsPostCreateCampaign: (
            data: PostCreateCampaignRequest,
            params: RequestParams = {},
        ) =>
            this.request<Campaign, void>({
                path: `/api/campaign`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCampaignsPutCampaignDetails
         * @request PUT:/api/campaign
         */
        takeInitiativeApiFeaturesCampaignsPutCampaignDetails: (
            data: PutCampaignDetailsRequest,
            params: RequestParams = {},
        ) =>
            this.request<Campaign, void>({
                path: `/api/campaign`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCampaignsGetCampaign
         * @request GET:/api/campaign/{CampaignId}
         */
        takeInitiativeApiFeaturesCampaignsGetCampaign: (campaignId: string, params: RequestParams = {}) =>
            this.request<GetCampaignResponse, void>({
                path: `/api/campaign/${campaignId}`,
                method: "GET",
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCampaignsPostJoinCampaign
         * @request POST:/api/campaign/join
         */
        takeInitiativeApiFeaturesCampaignsPostJoinCampaign: (
            data: JoinCampaignByJoinCodeRequest,
            params: RequestParams = {},
        ) =>
            this.request<Campaign, void>({
                path: `/api/campaign/join`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPutReadAndSaveCampaigns
         * @request PUT:/api/admin/readAndSave/campaigns
         */
        takeInitiativeApiFeaturesCombatsPutReadAndSaveCampaigns: (params: RequestParams = {}) =>
            this.request<any, any>({
                path: `/api/admin/readAndSave/campaigns`,
                method: "PUT",
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPutReprojectCombats
         * @request PUT:/api/admin/reproject/combat
         */
        takeInitiativeApiFeaturesCombatsPutReprojectCombats: (params: RequestParams = {}) =>
            this.request<any, any>({
                path: `/api/admin/reproject/combat`,
                method: "PUT",
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter
         * @request DELETE:/api/combat/initiative/character
         */
        takeInitiativeApiFeaturesCombatsDeleteInitiativeCharacter: (
            data: DeleteInitiativeCharacterRequest,
            params: RequestParams = {},
        ) =>
            this.request<CombatResponse, ErrorResponse | void>({
                path: `/api/combat/initiative/character`,
                method: "DELETE",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter
         * @request PUT:/api/combat/initiative/character
         */
        takeInitiativeApiFeaturesCombatsPutUpdateInitiativeCharacter: (
            data: PutUpdateInitiativeCharacterRequest,
            params: RequestParams = {},
        ) =>
            this.request<CombatResponse, ErrorResponse | void>({
                path: `/api/combat/initiative/character`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsDeleteStagedCharacter
         * @request DELETE:/api/combat/stage/character
         */
        takeInitiativeApiFeaturesCombatsDeleteStagedCharacter: (
            data: DeleteStagedCharacterRequest,
            params: RequestParams = {},
        ) =>
            this.request<CombatResponse, void>({
                path: `/api/combat/stage/character`,
                method: "DELETE",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter
         * @request PUT:/api/combat/stage/character
         */
        takeInitiativeApiFeaturesCombatsPutUpsertStagedCharacter: (
            data: PutUpsertStagedCharacterRequest,
            params: RequestParams = {},
        ) =>
            this.request<CombatResponse, ErrorResponse | void>({
                path: `/api/combat/stage/character`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsGetCombatHistory
         * @request GET:/api/combat/{id}/history
         */
        takeInitiativeApiFeaturesCombatsGetCombatHistory: (
            id: string,
            data: GetCombatRequest,
            params: RequestParams = {},
        ) =>
            this.request<GetCombatHistoryResponse, void>({
                path: `/api/combat/${id}/history`,
                method: "GET",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsGetCombats
         * @request GET:/api/combats
         */
        takeInitiativeApiFeaturesCombatsGetCombats: (data: GetCombatsRequest, params: RequestParams = {}) =>
            this.request<GetCombatsResponse, void>({
                path: `/api/combats`,
                method: "GET",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsGetCombat
         * @request GET:/api/combat/{Id}
         */
        takeInitiativeApiFeaturesCombatsGetCombat: (id: string, params: RequestParams = {}) =>
            this.request<CombatResponse, void>({
                path: `/api/combat/${id}`,
                method: "GET",
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsDeletePlannedCombat
         * @request DELETE:/api/combat/planned
         */
        takeInitiativeApiFeaturesCombatsDeletePlannedCombat: (
            data: DeletePlannedCombatRequest,
            params: RequestParams = {},
        ) =>
            this.request<void, void>({
                path: `/api/combat/planned`,
                method: "DELETE",
                body: data,
                type: ContentType.Json,
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPostPlannedCombat
         * @request POST:/api/combat/planned
         */
        takeInitiativeApiFeaturesCombatsPostPlannedCombat: (
            data: PostPlannedCombatRequest,
            params: RequestParams = {},
        ) =>
            this.request<PlannedCombat, void>({
                path: `/api/combat/planned`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsDeletePlannedCombatStage
         * @request DELETE:/api/campaign/planned-combat/stage
         */
        takeInitiativeApiFeaturesCombatsDeletePlannedCombatStage: (
            data: DeletePlannedCombatStageRequest,
            params: RequestParams = {},
        ) =>
            this.request<PlannedCombat, void>({
                path: `/api/campaign/planned-combat/stage`,
                method: "DELETE",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPostPlannedCombatStage
         * @request POST:/api/campaign/planned-combat/stage
         */
        takeInitiativeApiFeaturesCombatsPostPlannedCombatStage: (
            data: PostPlannedCombatStageRequest,
            params: RequestParams = {},
        ) =>
            this.request<PlannedCombat, ErrorResponse | void>({
                path: `/api/campaign/planned-combat/stage`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPutPlannedCombatStage
         * @request PUT:/api/campaign/planned-combat/stage
         */
        takeInitiativeApiFeaturesCombatsPutPlannedCombatStage: (
            data: PutPlannedCombatStageRequest,
            params: RequestParams = {},
        ) =>
            this.request<PlannedCombat, ErrorResponse | void>({
                path: `/api/campaign/planned-combat/stage`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc
         * @request DELETE:/api/campaign/planned-combat/stage/npc
         */
        takeInitiativeApiFeaturesCombatsDeletePlannedCombatNpc: (
            data: DeletePlannedCombatNpcRequest,
            params: RequestParams = {},
        ) =>
            this.request<PlannedCombat, ErrorResponse | void>({
                path: `/api/campaign/planned-combat/stage/npc`,
                method: "DELETE",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpc
         * @request POST:/api/campaign/planned-combat/stage/npc
         */
        takeInitiativeApiFeaturesCombatsPostPlannedCombatNpc: (
            data: PostPlannedCombatNpcRequest,
            params: RequestParams = {},
        ) =>
            this.request<PlannedCombat, ErrorResponse | void>({
                path: `/api/campaign/planned-combat/stage/npc`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPutPlannedCombatNpc
         * @request PUT:/api/campaign/planned-combat/stage/npc
         */
        takeInitiativeApiFeaturesCombatsPutPlannedCombatNpc: (
            data: PutPlannedCombatNpcRequest,
            params: RequestParams = {},
        ) =>
            this.request<PlannedCombat, ErrorResponse | void>({
                path: `/api/campaign/planned-combat/stage/npc`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPostEndTurn
         * @request POST:/api/combat/turn/end
         */
        takeInitiativeApiFeaturesCombatsPostEndTurn: (data: PostEndTurnRequest, params: RequestParams = {}) =>
            this.request<CombatResponse, ErrorResponse | void>({
                path: `/api/combat/turn/end`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPostFinishCombat
         * @request POST:/api/combat/finish
         */
        takeInitiativeApiFeaturesCombatsPostFinishCombat: (data: PostFinishCombatRequest, params: RequestParams = {}) =>
            this.request<CombatResponse, ErrorResponse | void>({
                path: `/api/combat/finish`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPostOpenCombat
         * @request POST:/api/combat/open
         */
        takeInitiativeApiFeaturesCombatsPostOpenCombat: (data: PostOpenCombatRequest, params: RequestParams = {}) =>
            this.request<CombatResponse, void>({
                path: `/api/combat/open`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative
         * @request POST:/api/combat/stage/roll
         */
        takeInitiativeApiFeaturesCombatsPostRollStagedCharactersIntoInitiative: (
            data: PostRollStagedCharactersIntoInitiativeRequest,
            params: RequestParams = {},
        ) =>
            this.request<CombatResponse, ErrorResponse | void>({
                path: `/api/combat/stage/roll`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPostStagePlannedCharacters
         * @request POST:/api/combat/stage/planned-character
         */
        takeInitiativeApiFeaturesCombatsPostStagePlannedCharacters: (
            data: PutStagePlannedCharactersRequest,
            params: RequestParams = {},
        ) =>
            this.request<CombatResponse, ErrorResponse | void>({
                path: `/api/combat/stage/planned-character`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPostStagePlayerCharacters
         * @request POST:/api/combat/stage/player-character
         */
        takeInitiativeApiFeaturesCombatsPostStagePlayerCharacters: (
            data: PostStagePlayerCharactersRequest,
            params: RequestParams = {},
        ) =>
            this.request<CombatResponse, ErrorResponse | void>({
                path: `/api/combat/stage/player-character`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesCombatsPostStartCombat
         * @request POST:/api/combat/start
         */
        takeInitiativeApiFeaturesCombatsPostStartCombat: (data: PostStartCombatRequest, params: RequestParams = {}) =>
            this.request<CombatResponse, ErrorResponse | void>({
                path: `/api/combat/start`,
                method: "POST",
                body: data,
                type: ContentType.Json,
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesAdminGetMaintenanceConfig
         * @request GET:/api/admin/maintenance
         */
        takeInitiativeApiFeaturesAdminGetMaintenanceConfig: (params: RequestParams = {}) =>
            this.request<MaintenanceConfig, any>({
                path: `/api/admin/maintenance`,
                method: "GET",
                format: "json",
                ...params,
            }),

        /**
         * No description
         *
         * @tags TakeInitiative.Api, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
         * @name TakeInitiativeApiFeaturesAdminPutMaintenanceConfig
         * @request PUT:/api/admin/maintenance
         */
        takeInitiativeApiFeaturesAdminPutMaintenanceConfig: (data: MaintenanceConfig, params: RequestParams = {}) =>
            this.request<void, any>({
                path: `/api/admin/maintenance`,
                method: "PUT",
                body: data,
                type: ContentType.Json,
                ...params,
            }),
    };
}
