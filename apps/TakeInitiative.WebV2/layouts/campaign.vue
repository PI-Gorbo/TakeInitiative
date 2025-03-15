<template>
    <div class="h-full w-full">
        <NuxtLayout name="app">
            <div class="flex h-full w-full justify-center">
                <div class="w-page">
                    <header class="flex flex-col gap-4">
                        <header class="font-NovaCut text-xl text-gold">
                            {{ campaign.state.campaign?.campaignName }}
                        </header>
                        <Tabs class="sticky">
                            <TabsList>
                                <TabsTrigger value="home">
                                    <FontAwesomeIcon :icon="faHome" />
                                </TabsTrigger>
                                <TabsTrigger value="combats">
                                    Combats
                                </TabsTrigger>
                                <TabsTrigger value="settings">
                                    Settings
                                </TabsTrigger>
                            </TabsList>
                            <TabsContent value="characters">
                                Make changes to your account here.
                            </TabsContent>
                            <TabsContent value="password">
                                Change your password here.
                            </TabsContent>
                        </Tabs>
                    </header>
                    <NuxtPage />
                </div>
            </div>
        </NuxtLayout>
    </div>
</template>

<script setup lang="ts">
    import { faHome } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { helpers } from "@typed-router";
    const campaign = useCampaignStore();
    const route = useRoute("app-campaigns-id");

    const result = useAsyncData(
        "campaign-layout-campaign-fetch",
        async () =>
            await campaign.setCampaignById(route.params.id).then(() => true),
        {
            watch: [() => route.params.id],
        }
    );
</script>
