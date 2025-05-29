<template>
    <LoadingFallback
        :key="store.combatId ?? ''"
        :isLoading="
            store.campaignQuery.isPending || store.combatQuery.isPending
        "
        class="h-full w-full max-h-full flex justify-center">
        <div class="lg:grid lg:grid-cols-3 w-page lg:gap-4 max-h-full pb-4">
            <div
                class="hidden lg:block lg:col-span-1 lg:col-start-1 overflow-auto">
                <CampaignCombatFullScreenCombatDetailsCard
                    :campaignId="route.params.campaignId"
                    :combatId="route.params.combatId" />
            </div>
            <div
                class="lg:col-span-2 lg:col-start-2 flex flex-col overflow-auto gap-4 h-full max-h-full">
                <div class="flex-1 overflow-auto">
                    <CampaignCombatMobileCombatDetailsTabs
                        :campaignId="route.params.campaignId"
                        :combatId="route.params.combatId">
                        <CampaignCombatInitiativeList
                            :campaignId="route.params.campaignId"
                            :combatId="route.params.combatId" />
                    </CampaignCombatMobileCombatDetailsTabs>
                </div>
                <div class="flex justify-between pb-1">
                    <div>
                        <AsyncButton
                            v-if="store.combatIsOpen"
                            label="Start"
                            loadingLabel="Starting..."
                            :icon="faFlag"
                            variant="outline"
                            class="interactable lg:hidden"
                            :click="combatControls.startCombat" />

                        <AsyncButton
                            v-else-if="store.combatIsStarted"
                            label="End Combat"
                            loadingLabel="Ending..."
                            :icon="faFlag"
                            variant="outline"
                            class="interactable lg:hidden"
                            :click="combatControls.finishCombat" />
                    </div>

                    <AsyncButton
                        v-if="store.combatIsStarted"
                        variant="destructive"
                        label="End Turn"
                        loadingLabel="Ending..."
                        :click="combatControls.endTurn"
                        :disabled="!isUsersTurn" />
                </div>
            </div>
        </div>
    </LoadingFallback>
</template>
<script setup lang="ts">
import { faFlag } from '@fortawesome/free-solid-svg-icons';

    const route = useRoute("app-campaigns-campaignId-combats-combatId");
    const combatId = computed(() => route.params.combatId);
    const userStore = useUserStore()
    const store = useCombatStore();
    const combatControls = useCombatControls(combatId);
    watchEffect(() => {
        console.log("triggered here");
        store.init(route.params.campaignId, combatId.value);
    });

    definePageMeta({
        layout: "main-app",
        pageType: "fixed",
        requiresAuth: true,
        layoutTransition: false,
    });


    const isUsersTurn = computed(() => {
        if (store.userIsDm) {
            return true;
        }

        if (store.combat?.initiativeIndex == null) {
            return false;
        }

        const characterWithCurrentInitiative =
        store.combat?.initiativeList[store.combat.initiativeIndex];
        if (
            characterWithCurrentInitiative?.playerId == userStore.state.user?.userId
        ) {
            return true;
        }
        return false;
    });
</script>
