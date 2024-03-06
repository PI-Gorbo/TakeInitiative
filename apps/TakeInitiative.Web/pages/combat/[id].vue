<template>
    <div class="w-full h-full cursor-pointer">
        <FormButton @clicked="() => refresh()" />
        <textarea
            disabled
            :value="JSON.stringify(combatStore.state, null, '\t')"
            class="w-full h-full bg-take-navy"
        />
    </div>
</template>
<script setup lang="ts">
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";

const route = useRoute();
const combatStore = useCombatStore();
const combatId = route.params.id;

const { refresh, pending, error } = useAsyncData("Combat", () => {
    return combatStore
        .setCombat(combatId as string)
        .then(() => {
            // If the user has not 'Joined' the combat yet, then make them join.
            if (combatStore.state.combat?.currentPlayers.find((x) => x.userId) == null) {
                return combatStore.joinCombat();
            }
        })
        .then(() => true);
});

onUnmounted(async () => {
    await combatStore.leaveCombat();
});

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>
