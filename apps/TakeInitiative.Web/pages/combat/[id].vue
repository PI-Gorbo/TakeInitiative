<template>
    <div class="w-full h-full">
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
    return combatStore.setCombat(combatId as string).then(() => true);
});

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>
