import type { InferType } from "yup";
import { yup } from "./HelperTypes";

// Campaign Member
const campaignMemberInfoValidator = yup.object({
    memberId: yup.string(),
    userId: yup.string(),
    isDungeonMaster: yup.boolean(),
});
export type CampaignMemberInfo = InferType<typeof campaignMemberInfoValidator>;

// Campaign
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
});
export type Campaign = InferType<typeof campaignValidator>;

// Character Initiative
export enum InitiativeStrategy {
    Fixed = 0,
    Roll = 1,
}
export const characterInitiativeValidator = yup.object({
    strategy: yup.mixed().oneOf(Object.values(InitiativeStrategy)),
    value: yup.string().required(),
});
export type CharacterInitiative = InferType<
    typeof characterInitiativeValidator
>;

// Character Health
export const characterHealthValidator = yup.object({
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
    armorClass: yup.number().nullable(),
});
export type ICharacter = InferType<typeof characterValidator>;
export const playerCharacterValidator = characterValidator.shape({
    playerId: yup.string().required(),
});
export type PlayerCharacter = InferType<typeof playerCharacterValidator>;

// Campaign Member
export const campaignMemberValidator = yup.object({
    id: yup.string().required(),
    userId: yup.string().required(),
    campaignId: yup.string().required(),
    isDungeonMaster: yup.boolean().required(),
    characters: yup.array(playerCharacterValidator),
});
export type CampaignMember = InferType<typeof campaignMemberValidator>;

// Planned Combat NPC
export const plannedCombatCharacterValidator = yup.object({
    id: yup.string(),
    name: yup.string().required("Please provide a name"),
    health: characterHealthValidator.notRequired(),
    armorClass: yup.number().notRequired(),
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
    state: yup.mixed().oneOf(Object.values(CombatState)),
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
export type Combat = InferType<typeof combatValidator>