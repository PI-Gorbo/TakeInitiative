<template>
    <main class="flex w-full flex-col gap-4">
        <Card class="border-2 border-dashed p-4">
            <CampaignEditIntroductionForm />
        </Card>
        <Card class="p-4">
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
                        <div>
                            <FontAwesomeIcon
                                :class="
                                    item.isDungeonMaster
                                        ? 'text-gold'
                                        : 'text-primary'
                                "
                                :icon="
                                    !item.isDungeonMaster ? faUserLarge : faCrown
                                " />
                            <label class="select-none">{{ item.username }}</label>
                        </div>
                    </AccordionTrigger>
                    <AccordionContent>
                        {{ item.resources }}
                    </AccordionContent>
                </AccordionItem>
            </Accordion>
        </Card>
    </main>
</template>
<script setup lang="ts">
    import { faCrown, faUserLarge, faUsers } from "@fortawesome/free-solid-svg-icons";
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
