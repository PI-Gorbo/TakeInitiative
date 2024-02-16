import type { InferType } from "yup";
import { yup } from "./HelperTypes";

const campaignMemberInfoValidator = yup.object({
    memberId: yup.string(),
    userId: yup.string(),
    isDungeonMaster: yup.boolean(),
});
export type CampaignMemberInfo = InferType<typeof campaignMemberInfoValidator>;

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

export const playerCharacterValidator = yup.object({
    id: yup.string().required(),
    playerId: yup.string().required(),
    name: yup.string().required(),
    health: yup.object().nullable(),
    initiative: yup.object().required(),
    armourClass: yup.number().nullable(),
});
export type PlayerCharacter = InferType<typeof playerCharacterValidator>;

export const campaignMemberValidator = yup.object({
    id: yup.string().required(),
    userId: yup.string().required(),
    campaignId: yup.string().required(),
    isDungeonMaster: yup.boolean().required(),
    currentCharacterId: yup.string().nullable(),
    characters: yup.array(playerCharacterValidator),
});
export type CampaignMember = InferType<typeof campaignMemberValidator>;

export const plannedCombatValidator = yup.object({
    id: yup.string().required(),
    campaignId: yup.string().required(),
    combatName: yup.string().required(),
    stages: yup.array(yup.object()),
});
export type PlannedCombat = InferType<typeof plannedCombatValidator>;
