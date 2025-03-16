<template>
    <main class="sm:grid w-full flex flex-col gap-4 sm:grid-cols-3">
        <div class="sm:col-span-2 sm:col-start-2">
            <Card class="border-2 border-dashed p-4 border-primary/50">
                <CampaignEditIntroductionForm />
            </Card>
        </div>
        <Card
            class="p-4 sm:col-span-1 sm:col-start-1 sm:row-start-1 border-primary/50">
            <header><FontAwesomeIcon :icon="faUsers" /> Players</header>
            <Accordion
                type="single"
                class="w-full"
                collapsible
                :defaultValue="userStore.state.user?.userId">
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
    </main>
</template>
<script setup lang="ts">
    import {
        faCrown,
        faUserLarge,
        faUsers,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const userStore = useUserStore();
    const campaignStore = useCampaignStore();

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const membersToDisplay = computed(() =>
        campaignStore.memberDtos.sort((a, b) => {
            if (a.isDungeonMaster && !b.isDungeonMaster) {
                return -1;
            }

            if (!a.isDungeonMaster && b.isDungeonMaster) {
                return 1;
            }

            if (a.username > b.username) {
                return -1;
            }

            return 1;
        })
    );
</script>
