import { navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin } from "~/utils/navigation/navigationHelpers";

export default defineNuxtRouteMiddleware(async (to) => {

    if (!to.meta.requiresAuth) return

    const userQuery = await useUserProfileQuery().suspense()
    // If the user is not logged in, return them to the 'does not require auth zone'
    if (userQuery.isError) {
        return navigateTo({
            path: "/login",
            query: {
                redirectTo: to.path,
            },
        });
    }

    await navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin(userQuery.data!);
});
