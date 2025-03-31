<template>
    <div class="h-full w-full">
        <NuxtLayout name="main-app">
            <div class="flex h-full w-full justify-center">
                <div class="w-page flex flex-col gap-4">
                    <header class="flex flex-col gap-4">
                        <header class="font-NovaCut text-xl text-gold">
                            {{ campaign.state.campaign?.campaignName }}
                        </header>
                        <div class="flex justify-between">
                            <Tabs
                                class="sticky"
                                :modelValue="currentTab"
                                @update:modelValue="
                                    (currentTab) => {
                                        router.push({
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
                                <TabsContent value="characters">
                                    Make changes to your account here.
                                </TabsContent>
                                <TabsContent value="password">
                                    Change your password here.
                                </TabsContent>
                            </Tabs>
                            <Button
                                variant="outline"
                                class="interactable"
                                @click="clickAddButton"
                                v-if="route.name === 'app-campaigns-id'">
                                <FontAwesomeIcon :icon="faPlus" />
                                <span>Add Players</span>
                            </Button>
                        </div>
                    </header>

                    <slot />
                </div>
            </div>
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
    import {
        helpers,
        type RoutesNamesList,
        type RoutesParamsRecord,
    } from "@typed-router";
    import { Axios, AxiosError } from "axios";

    const campaign = useCampaignStore();
    const router = useRouter();
    const route = useRoute("app-campaigns-id");

    const shareModalOpen = ref(false);

    useAsyncData(
        "campaign-layout-campaign-fetch",
        async () => {
            return await campaign
                .setCampaignById(route.params.id as string)
                .then(() => true)
                .catch((error: AxiosError) => {
                    router.push({ name: "app-campaigns" });
                    return false;
                });

            return false;
        },
        {
            watch: [() => route.params.id],
        }
    );

    const tabValues: {
        label?: string;
        icon?: IconDefinition;
        routeName: RoutesNamesList;
    }[] = [
        {
            icon: faHome,
            routeName: "app-campaigns-id",
        },
        {
            label: "Combats",
            routeName: "app-campaigns-id-combats",
        },
        {
            label: "Settings",
            routeName: "app-campaigns-id-settings",
        },
    ];

    const currentTab = computed(() => {
        const currentRoute = useRoute();
        if (currentRoute.name === "app-campaigns-id") {
            return currentRoute.name;
        }

        if (currentRoute.name.startsWith("app-campaigns-id-combats")) {
            return "app-campaigns-id-combats";
        }

        if (currentRoute.name.startsWith("app-campaigns-id-settings")) {
            return "app-campaigns-id-settings";
        }
    });

    function clickAddButton() {
        shareModalOpen.value = true;
    }
</script>
