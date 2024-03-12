export default defineNuxtRouteMiddleware(async (to, from) => {
    if (!process.client) return;
    const combatStore = useCombatStore();
    if (to?.name != "combat-id" && from?.name == "combat-id") {
        await combatStore.leaveCombat();
    } 
});
