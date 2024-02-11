import jwtUtils from "~/utils/jwtUtils";

export default defineNuxtRouteMiddleware(async (to) => {
    if (process.server == true) {
        return; // Ensure it only runs on client.
    }

	const userStore = useUserStore()
	const isLoggedIn = await userStore.isLoggedIn();
	
	// If the user is not logged in, return them to the 'does not require auth zone'
	if (!isLoggedIn && to.meta.requiresAuth) {
		console.log("Navigating to login")
		return navigateTo("/login");
	}

	// If the user is logged in, and the place they are going doesn't require auth, then we 
	// can redirect to the login page.
	if (isLoggedIn && !to.meta.requiresAuth) {
		console.log("navigating to home")
		return navigateTo("/")
	}
});
