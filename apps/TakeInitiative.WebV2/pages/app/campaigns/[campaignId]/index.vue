<template>
    <LoadingFallback
        container="main"
        :isLoading="campaignQuery.isLoading.value"
        class="">
        <template v-if="screenSize.isLargeScreen.value">
            <div class="grid grid-cols-3 pb-2 gap-4 h-full max-h-full">
                <div
                    class="col-span-2 col-start-2 flex flex-col gap-4 max-h-full h-full overflow-auto">
                    <Card
                        v-if="
                            campaignQuery.data.value?.userCampaignMember
                                .isDungeonMaster ||
                            (campaignQuery.data.value?.campaign
                                ?.campaignDescription != '' &&
                                campaignQuery.data.value?.campaign
                                    ?.campaignDescription != null)
                        "
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

                    <Card class="border-primary/50 overflow-auto">
                        <CardContent class="p-4">
                            <CampaignCombatHistorySection
                                :campaignId="route.params.campaignId" />
                        </CardContent>
                    </Card>
                </div>
                <div
                    class="col-span-1 col-start-1 row-start-1 flex flex-col gap-4">
                    <CampaignCombatJoinBanner
                        :campaignId="route.params.campaignId"
                        :combatInfo="
                            campaignQuery.data.value?.currentCombatInfo ?? null
                        " />
                    <Card class="p-4 border-primary/50">
                        <header>
                            <FontAwesomeIcon :icon="faUsers" /> Players
                        </header>
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
                                                item.userId ===
                                                campaignQuery.data.value
                                                    ?.campaign.ownerId
                                                    ? 'text-gold'
                                                    : 'text-primary'
                                            "
                                            :icon="
                                                !(
                                                    item.userId ===
                                                    campaignQuery.data.value
                                                        ?.campaign.ownerId
                                                )
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
        </template>
        <template v-else>
            <div class="w-full flex flex-col gap-4 pb-2">
                <CampaignCombatJoinBanner
                    :campaignId="route.params.campaignId"
                    :combatInfo="
                        campaignQuery.data.value?.currentCombatInfo ?? null
                    " />
                <Card
                    v-if="
                        campaignQuery.data.value?.userCampaignMember
                            .isDungeonMaster ||
                        (campaignQuery.data.value?.campaign
                            ?.campaignDescription != '' &&
                            campaignQuery.data.value?.campaign
                                ?.campaignDescription != null)
                    "
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
                                            item.userId ===
                                            campaignQuery.data.value?.campaign
                                                .ownerId
                                                ? 'text-gold'
                                                : 'text-primary'
                                        "
                                        :icon="
                                            !(
                                                item.userId ===
                                                campaignQuery.data.value
                                                    ?.campaign.ownerId
                                            )
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

                <Card class="border-primary/50 overflow-auto">
                    <CardContent class="p-4">
                        <CampaignCombatHistorySection
                            :campaignId="route.params.campaignId" />
                    </CardContent>
                </Card>
            </div>
        </template>
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
    import { getAllCombatsQuery } from "~/utils/queries/combats";

    const screenSize = useScreenSize();
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
            const aIsDm =
                a.userId === campaignQuery.data.value?.campaign.ownerId;
            const bIsDm =
                b.userId === campaignQuery.data.value?.campaign.ownerId;
            if (aIsDm && !bIsDm) {
                return -1;
            }

            if (!aIsDm && bIsDm) {
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
