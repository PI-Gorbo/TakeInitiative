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
                class="lg:col-span-2 lg:col-start-2 flex flex-col gap-4 max-h-full h-full">
                <CampaignCombatMobileCombatDetailsTabs
                    :campaignId="route.params.campaignId"
                    :combatId="route.params.combatId"
                    class="h-full">
                    <div class="flex flex-col gap-2 h-full">
                        <CampaignCombatInitiativeList
                            class="flex-1 overflow-auto"
                            :campaignId="route.params.campaignId"
                            :combatId="route.params.combatId" />
                        <div class="flex justify-between pb-1">
                            <div>
                                <template v-if="store.userIsDm">
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
                                </template>
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
                </CampaignCombatMobileCombatDetailsTabs>
            </div>
        </div>
    </LoadingFallback>
</template>
<script setup lang="ts">
    import { CampaignCombatMobileCombatDetailsTabs } from "#components";
    import { faFlag } from "@fortawesome/free-solid-svg-icons";
    import * as signalR from "@microsoft/signalr";
    import { useQueryClient } from "@tanstack/vue-query";
    import type { GetCombatResponse } from "~/utils/api/combat/getCombatRequest";
    import {
        getCampaignQuery,
        getCampaignQueryKey,
    } from "~/utils/queries/campaign";
    import { getCombatQueryKey } from "~/utils/queries/combats";
    import type { Combat } from "~/utils/types/models";

    const route = useRoute("app-campaigns-campaignId-combats-combatId");
    const combatId = computed(() => route.params.combatId);
    const userStore = useUserStore();
    const store = useCombatStore();
    const combatControls = useCombatControls(combatId);
    watchEffect(() => store.init(route.params.campaignId, combatId.value));

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
            characterWithCurrentInitiative?.playerId ==
            userStore.state?.userId
        ) {
            return true;
        }
        return false;
    });

    const queryClient = useQueryClient();
    const joinedCombatDetails = ref<{
        combatId: string;
        campaignId: string;
    } | null>(null);
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${useRuntimeConfig().public.axios.baseURL}/combatHub`, {
            accessTokenFactory: () => useCookie(".AspNetCore.Cookies").value!,
        })
        .withAutomaticReconnect()
        .build();

    connection.onreconnected(async () => {
        if (!joinedCombatDetails.value) return;

        queryClient.invalidateQueries({
            queryKey: getCombatQueryKey(
                joinedCombatDetails.value.campaignId,
                joinedCombatDetails.value.combatId
            ),
        });
        await connection.send(
            // Rejoin, as users are kicked from all groups on disconnect
            "joinCombat",
            userStore.state?.userId,
            joinedCombatDetails.value.combatId
        );
    });

    connection.on("combatUpdated", (combat: Combat) => {
        if (store.combat?.state != combat.state) {
            queryClient.invalidateQueries({
                queryKey: getCampaignQueryKey(
                    joinedCombatDetails.value?.campaignId!
                ),
            });
        }
        queryClient.setQueryData(
            getCombatQueryKey(
                joinedCombatDetails.value?.campaignId!,
                joinedCombatDetails.value?.combatId!
            ),
            {
                combat,
            } satisfies GetCombatResponse
        );
        return;
    });

    watch(
        combatId,
        async (newCombatId) => {
            if (!newCombatId) return;

            if (joinedCombatDetails.value) {
                await leaveCombat();
            }

            await joinCombat(newCombatId);
        },
        {
            immediate: true,
        }
    );

    onUnmounted(async () => {
        await leaveCombat();
        await connection.stop();
    });

    async function joinCombat(id: string) {
        if (connection.state !== signalR.HubConnectionState.Connected) {
            await connection.start();
        }

        return await connection
            .send(
                // Rejoin, as users are kicked from all groups on disconnect
                "joinCombat",
                userStore.state?.userId,
                id
            )
            .then(
                () =>
                    (joinedCombatDetails.value = {
                        campaignId: route.params.campaignId as string,
                        combatId: id,
                    })
            );
    }

    async function leaveCombat() {
        return await connection
            .send(
                "leaveCombat",
                userStore.state?.userId,
                joinedCombatDetails.value?.combatId
            )
            .then(() => (joinedCombatDetails.value = null));
    }
</script>
