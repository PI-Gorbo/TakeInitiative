<template>
    <main class="lg:grid w-full flex flex-col gap-4 lg:grid-cols-3">
        <div class="lg:col-span-2 lg:col-start-2">
            <Card class="border-2 border-dashed p-4 border-primary/50">
                <CampaignEditIntroductionForm />
            </Card>
        </div>
        <div class="lg:col-span-1 lg:col-start-1 lg:row-start-1">
            <Card class="p-4 border-primary/50">
                <header><FontAwesomeIcon :icon="faUsers" /> Players</header>
                <Accordion
                    type="single"
                    class="w-full"
                    collapsible
                    v-model:modelValue="openAccordionValue">
                    <AccordionItem
                        v-for="item in membersToDisplay"
                        :key="item.userId"
                        :value="item.userId">
                        <AccordionTrigger>
                            <div class="flex gap-2">
                                <FontAwesomeIcon
                                    :class="
                                        item.isDungeonMaster
                                            ? 'text-gold'
                                            : 'text-primary'
                                    "
                                    :icon="
                                        !item.isDungeonMaster
                                            ? faUserLarge
                                            : faCrown
                                    " />
                                <label class="select-none">
                                    {{ item.username }}
                                </label>
                            </div>
                        </AccordionTrigger>
                        <AccordionContent>
                            <CampaignPlayerResourcesSection
                                :userId="item.userId"
                                :characters="item.characters"
                                :resources="item.resources" />
                        </AccordionContent>
                    </AccordionItem>
                </Accordion>
            </Card>
        </div>
    </main>
</template>
<script setup lang="ts">
    import {
        faCrown,
        faUserLarge,
        faUsers,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useLocalStorage } from "@vueuse/core";

    const userStore = useUserStore();
    const campaignStore = useCampaignStore();
    const route = useRoute("app-campaigns-id");
    const openAccordionValue = useLocalStorage(
        `campaigns-${route.params.id}-accordion-current-user`,
        userStore.state.user?.userId
    );

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const membersToDisplay = computed(() =>
        campaignStore.memberDtos.sort((a, b) => {
            // Player should be first
            if (a.userId === userStore.state.user?.userId) {
                return -1;
            }

            if (b.userId === userStore.state.user?.userId) {
                return 1;
            }

            // Then dungeon master
            if (a.isDungeonMaster && !b.isDungeonMaster) {
                return -1;
            }

            if (!a.isDungeonMaster && b.isDungeonMaster) {
                return 1;
            }

            // Then order alphabetically
            if (a.username > b.username) {
                return -1;
            }

            return 1;
        })
    );
</script>
