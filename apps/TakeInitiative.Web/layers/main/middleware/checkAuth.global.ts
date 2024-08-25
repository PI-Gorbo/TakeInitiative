export default defineNuxtRouteMiddleware(async (to) => {
    const userStore = useUserStore();
    const isLoggedIn = await userStore.isLoggedIn();
    const navigate = useNavigator();
    // If the user is not logged in, return them to the 'does not require auth zone'
    if (!isLoggedIn && to.meta.requiresAuth) {
        console.log("navigating to login because the user is not logged in.");
        navigateTo({
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
