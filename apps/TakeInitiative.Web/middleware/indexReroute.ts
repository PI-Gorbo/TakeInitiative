export default defineNuxtRouteMiddleware(async (to) => {
    const userStore = useUserStore();
    if (userStore.state == null) {
        return;
    }

    // Reroute to the summary section of the first campaign in the user's list.
    if (
        userStore.state.memberCampaigns.length == 0 &&
        userStore.state.dmCampaigns.length == 0
    ) {
        if (to.meta.requiresAuth == false) {
            throw Error(
                "The destination of this middleware needs to be an authorized page.",
            );
        }
        return navigateTo("/createOrJoinCampaign");
    }

    return userStore.navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin();
});
