export default defineNuxtRouteMiddleware(async (to) => {
    const userStore = useUserStore();
    const isLoggedIn = await userStore.isLoggedIn();
    // If the user is not logged in, return them to the 'does not require auth zone'
    if (!isLoggedIn && to.meta.requiresAuth) {
        return navigateTo({
            path: "/login",
            query: {
                redirectTo: to.path,
            },
        });
    }

    if ((to.name == "login" || to.name == "signup") && isLoggedIn) {
        return userStore.navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin();
    }

    return;
});
