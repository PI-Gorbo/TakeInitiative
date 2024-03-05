import type { Combat } from "~/utils/types/models";

export const useCombatStore = defineStore("combatStore", () => {
	const userStore = useUserStore()
	const campaignStore = useCampaignStore()
    const api = useApi();
	
	const state = reactive<{
		combat: Combat | null
	}>({
		combat: null
	})

	async function setCombat(combatId: string) : Promise<void> {

		return await api.combat.get({combatId})
			.then(async (resp) => {
				state.combat = resp.combat
				userStore.setSelectedCampaign(resp.combat.campaignId)
				await campaignStore.setCampaignById(resp.combat.campaignId)
			})
	}

    return {
		state,
		setCombat
    };
});
