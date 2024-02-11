import jwtUtils from "~/utils/jwtUtils";

export default defineNuxtRouteMiddleware(async (to) => {
	console.log("Running");
	debugger;
  	const userStore = useUserStore();
	console.log("User store state",userStore.state.user)
	if (userStore.state.user == null) {
		return;
	}
	
	if (userStore.state.user.memberCampaigns.length == 0 && userStore.state.user.dmCampaigns.length == 0) {
		if (to.meta.requiresAuth == false) {
			throw Error("The destination of this middleware needs to be an authorized page.");
		}
		return navigateTo("/createOrJoinCampaign")
	}
});
