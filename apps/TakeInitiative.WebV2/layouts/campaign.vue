<template>
    <div class="h-full w-full max-h-full">
        <NuxtLayout name="main-app">
            <LoadingFallback
                :isLoading="campaignQuery.isLoading.value"
                class="flex w-full justify-center">
                <div class="w-page flex flex-col gap-4">
                    <header class="flex flex-col gap-4">
                        <div class="flex justify-between">
                            <Tabs
                                class="sticky"
                                :modelValue="currentTab"
                                @update:modelValue="
                                    (currentTab) => {
                                        navigateTo({
                                            name: currentTab as RoutesNamesList,
                                        });
                                    }
                                ">
                                <TabsList>
                                    <TabsTrigger
                                        v-for="tab in tabValues"
                                        :value="tab.routeName">
                                        <FontAwesomeIcon
                                            v-if="tab.icon"
                                            :icon="tab.icon" />
                                        <span v-if="tab.label">{{
                                            tab.label
                                        }}</span>
                                    </TabsTrigger>
                                </TabsList>
                            </Tabs>

                            <Popover>
                                <PopoverTrigger asChild>
                                    <Button
                                        variant="outline"
                                        class="interactable"
                                        v-if="
                                            baseLayoutRoute.name ===
                                            'app-campaigns-campaignId'
                                        ">
                                        <FontAwesomeIcon :icon="faPlus" />
                                        <span>Add Players</span>
                                    </Button>
                                </PopoverTrigger>
                                <PopoverContent>
                                    <header class="font-semibold">
                                        Add Players
                                    </header>
                                    <p class="text-muted-foreground">
                                        Players can join by visting the join
                                        link below, or entering the join code
                                        when prompted.
                                    </p>
                                    <CampaignShare />
                                </PopoverContent>
                            </Popover>
                        </div>
                    </header>

                    <div class="overflow-auto flex-1">
                        <slot />
                    </div>
                </div>
            </LoadingFallback>
        </NuxtLayout>
        <Dialog v-model:open="shareModalOpen">
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>Add Players</DialogTitle>
                    <DialogDescription>
                        Players can join by visting the join link below, or
                        entering the join code when prompted.
                    </DialogDescription>
                    <CampaignShare />
                </DialogHeader>
            </DialogContent>
        </Dialog>
    </div>
</template>

<script setup lang="ts">
    import {
        faHome,
        faPlus,
        type IconDefinition,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useQuery, useQueryClient } from "@tanstack/vue-query";
    import {
        helpers,
        type RoutesNamesList,
        type RoutesParamsRecord,
    } from "@typed-router";
    import { Axios, AxiosError } from "axios";
    import {
        getCampaignQuery,
        getCampaignQueryKey,
    } from "~/utils/queries/campaign";
    import * as signalR from "@microsoft/signalr";

    const route = useRoute();
    const baseLayoutRoute = useRoute("app-campaigns-campaignId");

    const queryClient = useQueryClient();
    const campaignQuery = useQuery(
        getCampaignQuery(() => baseLayoutRoute.params.campaignId as string)
    );

    const shareModalOpen = ref(false);
    const tabValues: {
        label?: string;
        icon?: IconDefinition;
        routeName: RoutesNamesList;
    }[] = [
        {
            icon: faHome,
            routeName: "app-campaigns-campaignId",
        },
        {
            label: "Combats",
            routeName: "app-campaigns-campaignId-combats",
        },
        {
            label: "Settings",
            routeName: "app-campaigns-campaignId-settings",
        },
    ] as const;

    const currentTab = computed(() => {
        if (route?.name === tabValues[0].routeName) {
            return route?.name;
        }

        if (route?.name.startsWith(tabValues[1].routeName)) {
            return tabValues[1].routeName;
        }

        if (route?.name.startsWith(tabValues[2].routeName)) {
            return tabValues[2].routeName;
        }
    });

    // Signal R
    // Start the connection.
    const joinedCampaignId = ref<string | null>(null);
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${useRuntimeConfig().public.axios.baseURL}/campaignHub`, {
            accessTokenFactory: () => useCookie(".AspNetCore.Cookies").value!,
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Debug)
        .build();
    connection.on("campaignStateUpdated", async () => {
        console.log("here");
        queryClient.invalidateQueries({
            queryKey: getCampaignQueryKey(joinedCampaignId.value),
        });
    });
    connection.on("campaignMemberStateUpdated", async () => {
        queryClient.invalidateQueries({
            queryKey: getCampaignQueryKey(joinedCampaignId.value),
        });
    });
    connection.onreconnected(async () => {
        // Join the campaign hub.
        await connection.send("Join", joinedCampaignId.value); // rejoin as you leave all groups on disconnect.
        queryClient.invalidateQueries({
            queryKey: getCampaignQueryKey(joinedCampaignId.value),
        });
    });

    watch(
        () => baseLayoutRoute.params.campaignId,
        async (newCampaignId) => {
            console.log("here", newCampaignId);
            if (!newCampaignId) return;

            if (joinedCampaignId.value) {
                await leaveCampaignHub();
            }

            await joinCampaignHub(newCampaignId);
        },
        {
            immediate: true,
        }
    );

    onUnmounted(async () => {
        await leaveCampaignHub();
        await connection.stop();
    });

    async function joinCampaignHub(id: string) {
        if (connection.state !== signalR.HubConnectionState.Connected) {
            await connection.start();
        }

        return await connection
            .send("Join", id)
            .then(() => (joinedCampaignId.value = id));
    }

    async function leaveCampaignHub() {
        return await connection
            .send("Leave", joinedCampaignId.value)
            .finally(() => (joinedCampaignId.value = null));
    }
</script>
