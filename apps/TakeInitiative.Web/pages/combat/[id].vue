<template>
    <div class="h-full w-full cursor-pointer">
        <FormButton @clicked="() => refresh()" />
        <textarea
            disabled
            :value="JSON.stringify(combatStore.state, null, '\t')"
            class="h-full w-full bg-take-navy"
        />
    </div>
</template>
<script setup lang="ts">
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";

const route = useRoute();

const combatStore = useCombatStore();
const combatId = route.params.id;

// const { refresh, pending, error } = await useAsyncData("Combat", async () => {
//     return await combatStore.setCombat(combatId as string);
// });

definePageMeta({
    requiresAuth: true,
    middleware: [
        async (to, from) => {
            if (to.name == "combat-id") {
                const combatStore = useCombatStore();
                const id = to.params.id as string;
                await combatStore
                    .setCombat(id as string)
                    .then(async () => await combatStore.joinCombat())
                    .catch(async () => {
                        await navigateTo("/");
                    });
            }
        },
    ],
});
</script>
