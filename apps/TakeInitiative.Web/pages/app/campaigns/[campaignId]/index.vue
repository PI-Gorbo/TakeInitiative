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
                </div>
                <div
                    class="col-span-1 col-start-1 row-start-1 flex flex-col gap-4">
                    <Card class="p-4 border-primary/50">
                        <header>
                            <FontAwesomeIcon :icon="faUsers" /> Players
                        </header>
                        <ul class="flex flex-col gap-2 pt-2">
                            <li
                                v-for="item in membersToDisplay"
                                :key="item.userId"
                                class="flex gap-2">
                                <FontAwesomeIcon
                                    :class="
                                        item.userId === campaignQuery.data.value?.campaign.ownerId
                                            ? 'text-gold'
                                            : 'text-primary'
                                    "
                                    :icon="
                                        item.userId === campaignQuery.data.value?.campaign.ownerId
                                            ? faCrown
                                            : faUserLarge
                                    " />
                                <span class="select-none">{{ item.username }}</span>
                            </li>
                        </ul>
                    </Card>
                </div>
            </div>
        </template>
        <template v-else>
            <div class="w-full flex flex-col gap-4 pb-2">
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
                    <ul class="flex flex-col gap-2 pt-2">
                        <li
                            v-for="item in membersToDisplay"
                            :key="item.userId"
                            class="flex gap-2">
                            <FontAwesomeIcon
                                :class="
                                    item.userId === campaignQuery.data.value?.campaign.ownerId
                                        ? 'text-gold'
                                        : 'text-primary'
                                "
                                :icon="
                                    item.userId === campaignQuery.data.value?.campaign.ownerId
                                        ? faCrown
                                        : faUserLarge
                                " />
                            <span class="select-none">{{ item.username }}</span>
                        </li>
                    </ul>
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
    import type { CampaignMemberDto } from "~/utils/api/campaign/getCampaignRequest";
    import { getCampaignQuery } from "~/utils/queries/campaign";

    const screenSize = useScreenSize();
    const route = useRoute("app-campaigns-campaignId");
    const userStore = useUserStore();
    const campaignQuery = useQuery(
        getCampaignQuery(() => route.params.campaignId as string)
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
                username: userStore.state?.username!,
            },
        ] satisfies CampaignMemberDto[];
    });

    const membersToDisplay = computed(() =>
        memberDtos.value.sort((a, b) => {
            // Player should be first
            if (a.userId === userStore.state?.userId) {
                return -1;
            }

            if (b.userId === userStore.state?.userId) {
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
