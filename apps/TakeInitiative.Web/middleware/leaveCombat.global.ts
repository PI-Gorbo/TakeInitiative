export default defineNuxtRouteMiddleware(async (to, from) => {
	const combatStore = useCombatStore();
	if (from.name == "combat-id") {
		await combatStore.leaveCombat();
	}
});
