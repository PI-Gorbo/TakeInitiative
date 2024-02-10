import jwtUtils from "~/utils/jwtUtils";

export default defineNuxtRouteMiddleware(async (to) => {
    if (process.server == true) {
        return; // Ensure it only runs on client.
    }

	const userStore = useUserStore()
	const isLoggedIn = await userStore.isLoggedIn();
	console.log(to, isLoggedIn)
	if (!isLoggedIn && to.meta.requiresAuth) {
		return navigateTo("/login");
	}
	if (isLoggedIn) console.log("VALID JWT");
});
