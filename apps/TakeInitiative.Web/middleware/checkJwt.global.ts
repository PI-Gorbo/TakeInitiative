import jwtUtils from "~/utils/jwtUtils";

export default defineNuxtRouteMiddleware((to) => {
    if (process.server == true) {
        return; // Ensure it only runs on client.
    }

	const userStore = useUserStore()

	
    // Check if the user is the minimum level of authenticated.
	console.log("Checking if the user's jwt is valid...")
    const token = window.localStorage.getItem(jwtUtils.LocalStorageJwtKey);
    if (token != null && !jwtUtils.isValidJwt(token) && to.meta.requiresAuth) {
		console.log("Invalid Jwt. Reason:")
		console.log("Token valid? : ", jwtUtils.isValidJwt(token))
		console.log("Page requires auth: ", jwtUtils.isValidJwt(token))
        return navigateTo("/login");
    }
    console.log("VALID JWT");
});
