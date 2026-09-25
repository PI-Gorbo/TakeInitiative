import type { Campaign, CampaignMember } from "./api/types";

/** The caller's own member entry in a campaign. */
export function currentMember(campaign: Campaign | null | undefined): CampaignMember | undefined {
    return campaign?.members.find((m) => m.memberId === campaign.currentMemberId);
}
