import { z } from "zod";

// Role: a member's role in a campaign.
export const roleValidator = z.enum(["DM", "Player"]);
export type Role = z.infer<typeof roleValidator>;

// Campaign Member: one entry of the Campaign projection's member list.
export const campaignMemberValidator = z.object({
    memberId: z.string(),
    userId: z.string(),
    username: z.string(),
    role: roleValidator,
    joinedAt: z.string(),
    isOwner: z.boolean(),
});
export type CampaignMember = z.infer<typeof campaignMemberValidator>;

// Campaign: as returned by GET /api/campaigns/{id} and the campaign writes.
export const campaignValidator = z.object({
    id: z.string(),
    name: z.string(),
    joinCode: z.string(),
    ownerMemberId: z.string(),
    createdAt: z.string(),
    currentMemberId: z.string(),
    members: z.array(campaignMemberValidator),
});
export type Campaign = z.infer<typeof campaignValidator>;

// Campaign Summary: one of the caller's campaigns, from GET /api/campaigns.
export const campaignSummaryValidator = z.object({
    id: z.string(),
    name: z.string(),
    role: roleValidator,
    isOwner: z.boolean(),
    memberCount: z.number(),
});
export type CampaignSummary = z.infer<typeof campaignSummaryValidator>;

/** The caller's own member entry in a campaign. */
export function currentMember(campaign: Campaign | null | undefined): CampaignMember | undefined {
    return campaign?.members.find((m) => m.memberId === campaign.currentMemberId);
}
