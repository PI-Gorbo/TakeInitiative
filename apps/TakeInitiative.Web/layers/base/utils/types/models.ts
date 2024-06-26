import type { InferType } from "yup";
import { yup } from "./HelperTypes";

type ValuesOf<T extends {}> = T[keyof T];

// Campaign Member
const campaignMemberInfoValidator = yup.object({
    memberId: yup.string(),
    userId: yup.string(),
    isDungeonMaster: yup.boolean(),
});
export type CampaignMemberInfo = InferType<typeof campaignMemberInfoValidator>;

// Campaign
export const HealthDisplayOptionsEnum = {
    RealValue: 0,
    HealthyBloodied: 1,
    Hidden: 2,
} as const;
export const HealthDisplayOptionValueKeyMap = {
    0: "RealValue",
    1: "HealthyBloodied",
    2: "Hidden",
};
export type HealthDisplayOptionKeys = keyof typeof HealthDisplayOptionsEnum;
export type HealthDisplayOptionValues =
    (typeof HealthDisplayOptionsEnum)[HealthDisplayOptionKeys];

export const ArmourClassDisplayOptionsEnum = {
    RealValue: 0,
    Hidden: 2,
} as const;
export const ArmourClassDisplayOptionValueKeyMap = {
    0: "RealValue",
    2: "Hidden",
};
export type ArmourClassDisplayOptionKeys =
    keyof typeof ArmourClassDisplayOptionsEnum;
export type ArmourClassDisplayOptionValues =
    (typeof ArmourClassDisplayOptionsEnum)[ArmourClassDisplayOptionKeys];

const campaignSettingsValidator = yup.object({
    combatHealthDisplaySettings: yup
        .object({
            dmCharacterDisplayMethod: yup
                .mixed<HealthDisplayOptionValues>()
                .required(),
            otherPlayerCharacterDisplayMethod: yup
                .mixed<HealthDisplayOptionValues>()
                .required(),
        })
        .required(),
    combatArmourClassDisplaySettings: yup.object({
        dmCharacterDisplayMethod: yup
            .mixed<ArmourClassDisplayOptionValues>()
            .required(),
        otherPlayerCharacterDisplayMethod: yup
            .mixed<ArmourClassDisplayOptionValues>()
            .required(),
    }),
});
export type CampaignSettings = InferType<typeof campaignSettingsValidator>;

export const campaignValidator = yup.object({
    id: yup.string().required(),
    ownerId: yup.string().required(),
    campaignName: yup.string().required(),
    campaignDescription: yup.string(),
    campaignResources: yup.string(),
    plannedCombatIds: yup.array(yup.string()),
    campaignMemberInfo: yup.array(campaignMemberInfoValidator),
    activeCombatId: yup.string().nullable(),
    createdTimestamp: yup.string(),
    campaignSettings: campaignSettingsValidator.required(),
});
export type Campaign = InferType<typeof campaignValidator>;

// Character Initiative
export enum InitiativeStrategy {
    Fixed = 0,
    Roll = 1,
}
export const characterInitiativeValidator = yup.object({
    strategy: yup
        .mixed<InitiativeStrategy>()
        .oneOf(
            Object.values(InitiativeStrategy).map(
                (x) => x as InitiativeStrategy,
            ),
        ),
    value: yup.string().required("An initiative value is required"),
});
export type CharacterInitiative = InferType<
    typeof characterInitiativeValidator
>;

// Character Health
export const characterHealthValidator = yup.object({
    hasHealth: yup.boolean().required(),
    maxHealth: yup.number().nullable(),
    currentHealth: yup.number().required(),
});
export type CharacterHealth = InferType<typeof characterHealthValidator>;

// Player Character
export const characterValidator = yup.object({
    id: yup.string().required(),
    name: yup.string().required(),
    health: characterHealthValidator.nullable(),
    initiative: characterInitiativeValidator,
    armourClass: yup.number().nullable(),
});
export type ICharacter = InferType<typeof characterValidator>;
export const playerCharacterValidator = characterValidator.shape({
    playerId: yup.string().required(),
});
export type PlayerCharacter = InferType<typeof playerCharacterValidator>;

// Campaign Member
export const campaignMemberResourceValidator = yup.object({
    name: yup.string().required(),
    link: yup.string().required(),
    lastModifiedTimestamp: yup.string(),
});
export type CampaignMemberResource = InferType<
    typeof campaignMemberResourceValidator
>;
export const campaignMemberValidator = yup.object({
    id: yup.string().required(),
    userId: yup.string().required(),
    campaignId: yup.string().required(),
    isDungeonMaster: yup.boolean().required(),
    characters: yup.array(playerCharacterValidator),
    resources: yup.array(campaignMemberResourceValidator),
});
export type CampaignMember = InferType<typeof campaignMemberValidator>;

// Planned Combat NPC
export const plannedCombatCharacterValidator = yup.object({
    id: yup.string(),
    name: yup.string().required("Please provide a name"),
    health: characterHealthValidator.notRequired(),
    armourClass: yup.number().notRequired(),
    initiative: characterInitiativeValidator,
    quantity: yup.number(),
});
export type PlannedCombatCharacter = InferType<
    typeof plannedCombatCharacterValidator
>;

// Planned Combat Stage
export const plannedCombatStageValidator = yup.object({
    id: yup.string().required(),
    name: yup.string().required(),
    npcs: yup.array(plannedCombatCharacterValidator),
});
export type PlannedCombatStage = InferType<typeof plannedCombatStageValidator>;

// Planned Combat
export const plannedCombatValidator = yup.object({
    id: yup.string().required(),
    campaignId: yup.string().required(),
    combatName: yup.string().required(),
    stages: yup.array(plannedCombatStageValidator),
});
export type PlannedCombat = InferType<typeof plannedCombatValidator>;

// Combat
export enum CombatState {
    Open = 0,
    Started = 1,
    Paused = 2,
    Finished = 3,
}

export const combatTimingRecordValidator = yup.object({
    startTime: yup.string(),
    endTime: yup.string().nullable(),
});
export type CombatTiming = InferType<typeof combatTimingRecordValidator>;
export const playerDtoValidator = yup.object({
    userId: yup.string().required(),
});
export type PlayerDto = InferType<typeof playerDtoValidator>;

export const CharacterOriginOptions = {
    PlayerCharacter: 0,
    PlannedCharacter: 1,
    CustomCharacter: 2,
} as const;
export const combatCharacterValidator = yup.object({
    id: yup.string().required(),
    characterOriginDetails: yup.object({
        characterOrigin: yup
            .mixed()
            .oneOf(Object.values(CharacterOriginOptions)),
        id: yup.string().nullable(),
    }),
    playerId: yup.string().required(),
    initiativeValue: yup.array(yup.number().required()).nullable(),
    hidden: yup.boolean().required(),
    name: yup.string().required(),
    health: characterHealthValidator.nullable(),
    initiative: characterInitiativeValidator.required(),
    armourClass: yup.number().nullable(),
    copyNumber: yup.number().nullable(),
});
export type CombatCharacter = InferType<typeof combatCharacterValidator>;

export const combatValidator = yup.object({
    id: yup.string().required(),
    campaignId: yup.string().required(),
    state: yup
        .mixed<CombatState>()
        .oneOf(Object.values(CombatState) as CombatState[]),
    combatName: yup.string().required(),
    dungeonMaster: yup.string().required(),
    combatLogs: yup.array(yup.string()).required(),
    currentPlayers: yup.array(playerDtoValidator).required(),
    plannedStages: yup.array(plannedCombatStageValidator).required(),
    initiativeList: yup.array(combatCharacterValidator).required(),
    stagedList: yup.array(combatCharacterValidator).required(),
    initiativeIndex: yup.number(),
    roundNumber: yup.string().nullable(),
});
export type Combat = InferType<typeof combatValidator>;
