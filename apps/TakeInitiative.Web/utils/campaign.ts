import type { Campaign, CampaignMember } from "./api/types";

/** Mirrors the API's `Campaign.NameMaxLength`. */
export const NAME_MAX_LENGTH = 100;

/** The caller's own member entry in a campaign. */
export function currentMember(campaign: Campaign | null | undefined): CampaignMember | undefined {
    return campaign?.members.find((m) => m.memberId === campaign.currentMemberId);
}

/** Whether the caller may manage the campaign: settings management is a DM's (design §1). */
export function canManageCampaign(campaign: Campaign | null | undefined): boolean {
    return currentMember(campaign)?.role === "DM";
}
