import type { LocationQueryValue } from "vue-router";

export default defineNuxtRouteMiddleware(async (to) => {
    const redirectToPath = to.query.redirectTo as LocationQueryValue;
    const userStore = useUserStore();
    const isLoggedIn = await userStore.isLoggedIn();
    if (redirectToPath != null) {
        return navigateTo(redirectToPath);
    } else {
        return userStore.navigateToFirstAvailableCampaign();
    }

    return;
});
