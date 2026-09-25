import { z } from "zod";

// Campaign Member
const campaignMemberInfoValidator = z.object({
    memberId: z.string(),
    userId: z.string(),
});
export type CampaignMemberInfo = z.infer<typeof campaignMemberInfoValidator>;

// Campaign
export const campaignValidator = z
    .object({
        id: z.string(),
        ownerId: z.string(),
        campaignName: z.string(),
        campaignDescription: z.string(),
        campaignMemberInfo: z.array(campaignMemberInfoValidator),
        createdTimestamp: z.string(),
    })
    .required({
        id: true,
        ownerId: true,
        campaignName: true,
    });
export type Campaign = z.infer<typeof campaignValidator>;

// Campaign Member
export const campaignMemberValidator = z
    .object({
        id: z.string(),
        userId: z.string(),
        campaignId: z.string(),
        isDungeonMaster: z.boolean(),
    })
    .required({
        id: true,
        userId: true,
        campaignId: true,
        isDungeonMaster: true,
    });
export type CampaignMember = z.infer<typeof campaignMemberValidator>;
