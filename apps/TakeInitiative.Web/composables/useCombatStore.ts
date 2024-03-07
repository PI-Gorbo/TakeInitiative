import type { Combat } from "~/utils/types/models";
import * as signalR from "@microsoft/signalr";
import { connect } from "http2";

export const useCombatStore = defineStore("combatStore", () => {
	const userStore = useUserStore()
	const campaignStore = useCampaignStore()
    const api = useApi();

	// Start the connection.
	var connection = new signalR.HubConnectionBuilder()
		.withUrl(`${useRuntimeConfig().public.axios.baseURL}/combatHub`)
		.build();

	connection.on("combatUpdated", (combat: Combat) => state.combat = combat)

	connection.onclose(async function() {
		await leaveCombat();
	});
	
	const state = reactive<{
		combat: Combat | null
		signalRError: string | null
	}>({
		combat: null,
		signalRError: null
	})

	async function setCombat(combatId: string) : Promise<void> {
		return await api.combat.get({combatId})
			.then(async (resp) => {
				state.combat = resp.combat
				userStore.setSelectedCampaign(resp.combat.campaignId)
				await campaignStore.setCampaignById(resp.combat.campaignId)
				if (!connection.connectionId) {
					await connection.start().catch((error) => state.signalRError = error)
						.then(() => joinCombat())
				}
			})
	}

	async function joinCombat() : Promise<void> {

		const userId = userStore.state.user?.userId;
		if (state.combat?.currentPlayers.find(x => x.userId) != null) {
			return Promise.resolve()
		}

		return await connection.send('joinCombat', userId, state.combat?.id)
	}

	async function leaveCombat() : Promise<void> {
		const userId = userStore.state.user?.userId;
		if (state.combat?.currentPlayers.find(x => x.userId) == null) {
			return Promise.resolve()
		}

		return await connection.send('leaveCombat', userId, state.combat?.id)
	}

    return {
		state,
		setCombat,
		joinCombat,
		leaveCombat
    };
});
