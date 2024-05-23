export default defineNuxtRouteMiddleware(async (to) => {

    console.log("In index reroute")
    const userStore = useUserStore();
    if (userStore.state.user == null) {
        return;
    }

    // Reroute to the summary section of the first campaign in the user's list.
    if (
        userStore.state.user.memberCampaigns.length == 0 &&
        userStore.state.user.dmCampaigns.length == 0
    ) {
        if (to.meta.requiresAuth == false) {
            throw Error(
                "The destination of this middleware needs to be an authorized page.",
            );
        }
        return navigateTo("/createOrJoinCampaign");
    }

    return userStore.navigateToFirstAvailableCampaign();
});
