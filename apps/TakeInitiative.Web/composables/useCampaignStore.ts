import type { Campaign } from './../utils/types/models';

export const useCampaignStore = defineStore("campaignStore", () => {
	const api = useApi()
	const userStore = useUserStore()
	const state = reactive({
		campaign: null as Campaign | null,
	}) 

	async function init() : Promise<void> {

		return await userStore.isLoggedIn()
			.then(async (loggedIn) => {

				if (!loggedIn) {
					return Promise.reject("User is not logged in")
				}

				// Initialize Dependencies.
				const campaignId: string | undefined = state.campaign?.id ?? userStore.state.user?.dmCampaigns.concat(userStore.state.user.memberCampaigns)[0].campaignId;
				if (campaignId == null) {
					return Promise.reject("User store is empty. Cannot initialize the campaign store.")
				}
				
				return await setCampaignById(campaignId)
			})

	}

	async function fetchCampaign(campaignId: string) : Promise<Campaign> {
		return await api.campaign.get({campaignId})
	}

	const setCampaignById = async (campaignId: string) : Promise<void> => fetchCampaign(campaignId).then(setCampaign)
	async function setCampaign(campaign: Campaign) : Promise<void> {
		state.campaign = campaign
	}

	return {
		state,
		init,
		setCampaign,
		setCampaignById
	}

})