<template>
    <LoadingFallback
        container="main"
        :isLoading="campaignQuery.isLoading.value">
        <div class="lg:grid w-full flex flex-col gap-4 lg:grid-cols-3 pb-2">
            <div class="lg:col-span-2 lg:col-start-2 flex flex-col gap-4">
                <CampaignCombatJoinBanner
                    class="block lg:hidden"
                    :campaignId="route.params.campaignId"
                    :combatInfo="
                        campaignQuery.data.value?.currentCombatInfo ?? null
                    " />
                <Card
                    class="p-4 border-primary/50"
                    :class="{
                        'border-2 border-dashed':
                            campaignQuery.data.value?.campaign
                                ?.campaignDescription == null ||
                            campaignQuery.data.value?.campaign
                                ?.campaignDescription == '',
                    }">
                    <CampaignEditIntroductionForm />
                </Card>
            </div>
            <div
                class="lg:col-span-1 lg:col-start-1 lg:row-start-1 flex flex-col gap-4">
                <CampaignCombatJoinBanner
                    class="hidden lg:block"
                    :campaignId="route.params.campaignId"
                    :combatInfo="
                        campaignQuery.data.value?.currentCombatInfo ?? null
                    " />
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
                            <AccordionContent class="pl-4">
                                <CampaignPlayerResourcesSection
                                    :userId="item.userId"
                                    :characters="item.characters"
                                    :resources="item.resources" />
                            </AccordionContent>
                        </AccordionItem>
                    </Accordion>
                </Card>
            </div>
        </div>
    </LoadingFallback>
</template>
<script setup lang="ts">
    import {
        faCrown,
        faUserLarge,
        faUsers,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useQuery } from "@tanstack/vue-query";
    import { useLocalStorage } from "@vueuse/core";
    import type { CampaignMemberDto } from "~/utils/api/campaign/getCampaignRequest";
    import { getCampaignQuery } from "~/utils/queries/campaign";

    const route = useRoute("app-campaigns-campaignId");
    const userStore = useUserStore();
    const campaignQuery = useQuery(
        getCampaignQuery(() => route.params.campaignId as string)
    );
    const openAccordionValue = useLocalStorage(
        `campaigns-${route.params.campaignId}-accordion-current-user`,
        userStore.state.user?.userId
    );

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    // Member Details
    const memberDtos: ComputedRef<CampaignMemberDto[]> = computed(() => {
        if (!campaignQuery.isSuccess.value) {
            return [];
        }

        return [
            ...campaignQuery.data.value!.campaignMembers,
            {
                ...campaignQuery.data.value!.userCampaignMember,
                username: userStore.state.user?.username!,
            },
        ] satisfies CampaignMemberDto[];
    });

    const membersToDisplay = computed(() =>
        memberDtos.value.sort((a, b) => {
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
