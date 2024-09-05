<template>
    <main>
        <header class="flex items-center gap-2 px-2">
            <FormButton
                icon="arrow-left"
                size="sm"
                label="back"
                buttonColour="take-navy"
                @click="
                    () =>
                        navigator.toCampaignTab(
                            route.params.id as string,
                            'summary',
                        )
                "
            />
            <label class="text-lg"> '{{ combatDetails }}' Combat History</label>
        </header>
        <CombatHistorySection :combatId="parsedCombatId" />
    </main>
</template>
<script setup lang="ts">
const navigator = useNavigator();
const route = useRoute();
const campaignStore = useCampaignStore();
const combatId = route.params.combatId;
const parsedCombatId = computed(() => combatId as string);
const combatDetails = computed(
    () =>
        campaignStore.state.combatHistory?.find(
            (x) => x.combatId == parsedCombatId.value,
        )?.combatName,
);

// Page info
definePageMeta({
    requiresAuth: true,
    layout: "campaign-tabs",
});
</script>
