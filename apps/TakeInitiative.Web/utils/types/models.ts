import * as yup from "yup";
const campaignMemberInfoValidator = yup.object({
    memberId: yup.string(),
    userId: yup.string(),
    isDungeonMaster: yup.boolean(),
});
export type CampaignMemberInfo = yup.InferType<
    typeof campaignMemberInfoValidator
>;

export const campaignValidator = yup.object({
    id: yup.string().required(),
    ownerId: yup.string().required(),
    campaignName: yup.string().required(),
    campaignDescription: yup.string().required(),
    campaignResources: yup.string().required(),
    plannedCombatIds: yup.array(yup.string()).required(),
    campaignMemberInfo: yup.array(campaignMemberInfoValidator).required(),
    activeCombatId: yup.string().notRequired(),
    combatIds: yup.array(yup.string()).required(),
    createdTimestamp: yup.string().required(),
});
export type Campaign = yup.InferType<typeof campaignValidator>;
