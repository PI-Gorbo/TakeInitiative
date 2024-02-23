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
    combatIds: yup.array(yup.string()),
    createdTimestamp: yup.string(),
});
export type Campaign = InferType<typeof campaignValidator>;

// Player Character
export const playerCharacterValidator = yup.object({
    id: yup.string().required(),
    playerId: yup.string().required(),
    name: yup.string().required(),
    health: yup.object().nullable(),
    initiative: yup.object().required(),
    armourClass: yup.number().nullable(),
});
export type PlayerCharacter = InferType<typeof playerCharacterValidator>;

// Campaign Member
export const campaignMemberValidator = yup.object({
    id: yup.string().required(),
    userId: yup.string().required(),
    campaignId: yup.string().required(),
    isDungeonMaster: yup.boolean().required(),
    currentCharacterId: yup.string().nullable(),
    characters: yup.array(playerCharacterValidator),
});
export type CampaignMember = InferType<typeof campaignMemberValidator>;

// Character Health
export const characterHealthValidator = yup.object({
    maxHealth: yup.number().nullable(),
    currentHealth: yup.number().required(),
});
export type CharacterHealth = InferType<typeof characterHealthValidator>;

export enum InitiativeStrategy {
    Fixed = 0,
    Roll = 1,
}

// Character Initiative
export const characterInitiativeValidator = yup.object({
    strategy: yup.mixed().oneOf(Object.values(InitiativeStrategy)),
    value: yup.string().required(),
});
export type CharacterInitiative = InferType<
    typeof characterInitiativeValidator
>;

// Planned Combat NPC
export const plannedCombatNonPlayerCharacterValidator = yup.object({
    name: yup.string().required("Please provide a name"),
    health: characterHealthValidator.nullable(),
    armorClass: yup.number().nullable(),
    initiative: characterInitiativeValidator,
    quantity: yup.number(),
});
export type PlannedCombatNonPlayerCharacter = InferType<
    typeof plannedCombatNonPlayerCharacterValidator
>;

// Planned Combat Stage
export const plannedCombatStageValidator = yup.object({
    id: yup.string().required(),
    name: yup.string().required(),
    NPCs: yup.array(plannedCombatNonPlayerCharacterValidator),
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
