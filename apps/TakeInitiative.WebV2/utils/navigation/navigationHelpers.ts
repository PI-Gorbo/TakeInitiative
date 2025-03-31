import type { GetUserResponse } from "../api/user/getUserRequest";

export async function navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin(user: GetUserResponse) {
    if (user == null) {
        return;
    }

    // Get the first campaign available
    const campaign = user.memberCampaigns.concat(
        user.dmCampaigns
    )[0];

    if (campaign == null) {
        return useNavigator().toCreateOrJoinCampaign();
    }

    return useNavigator().toCampaign(campaign.campaignId);
}