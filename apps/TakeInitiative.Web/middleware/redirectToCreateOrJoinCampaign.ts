export default defineNuxtRouteMiddleware(async (to) => {
  	const userStore = useUserStore();
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
