import { helpers } from "@typed-router";

export default defineNuxtRouteMiddleware(async (to) => {

    if (to.path === '/') {
        return;
    }
    const userStore = useUserStore();
    await userStore.init();

    // If the user is not logged in, return them to the 'does not require auth zone'
    if (!userStore.isLoggedIn && to.meta.requiresAuth) {
        return navigateTo({
            path: "/login",
            query: {
                redirectTo: to.path,
            },
        });
    }

    if ((to.name == "login" || to.name == "signup") && userStore.isLoggedIn) {
        return userStore.navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin();
    }

    return;
});
