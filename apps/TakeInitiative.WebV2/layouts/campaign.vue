<template>
    <div class="h-full w-full">
        <NuxtLayout name="main-app">
            <LoadingFallback
                :isLoading="campaignQuery.isLoading.value"
                class="flex w-full justify-center">
                <div class="w-page flex flex-col gap-4">
                    <header class="flex flex-col gap-4">
                        <header
                            class="font-NovaCut text-xl text-gold hidden sm:block">
                            {{
                                campaignQuery.data.value?.campaign?.campaignName
                            }}
                        </header>
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

                            <Button
                                variant="outline"
                                class="interactable"
                                @click="clickAddButton"
                                v-if="
                                    baseLayoutRoute.name ===
                                    'app-campaigns-campaignId'
                                ">
                                <FontAwesomeIcon :icon="faPlus" />
                                <span>Add Players</span>
                            </Button>
                        </div>
                    </header>

                    <slot />
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
    import { useQuery } from "@tanstack/vue-query";
    import {
        helpers,
        type RoutesNamesList,
        type RoutesParamsRecord,
    } from "@typed-router";
    import { Axios, AxiosError } from "axios";
    import { getCampaignQuery } from "~/utils/queries/campaign";

    const route = useRoute();
    const baseLayoutRoute = useRoute("app-campaigns-campaignId");
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

    function clickAddButton() {
        shareModalOpen.value = true;
    }
</script>
