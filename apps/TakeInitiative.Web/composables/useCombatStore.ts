import type { Combat } from "~/utils/types/models";

export const useCombatStore = defineStore("combatStore", () => {
    const api = useApi();
	
	const state = reactive<{
		combat: Combat | null
	}>({
		combat: null
	})

	async function setCombat(combatId: string) : Promise<void> {
		return await api.combat.get({combatId})
			.then((resp) => {
				state.combat = resp.combat
			})
	}

    return {
		setCombat
    };
});
