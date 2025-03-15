<template>
    <div class="flex h-full w-full justify-center">
        <main class="w-page flex flex-col gap-4">
            <template
                v-if="(userStore.state.user?.dmCampaigns ?? []).length > 0">
                <header class="font-NovaCut text-xl text-gold sm:text-2xl">
                    My Campaigns
                </header>
                <ul>
                    <li v-for="campaign in userStore.campaignList">
                        <NuxtLink
                            :to="`/app/campaigns/${campaign.campaignId}`">
                            <Card
                                :class="[
                                    'interactable group shadow-solid-sm transition-colors hover:border-primary',
                                    {
                                        'border-destructive':
                                            campaign.currentCombatName != null,
                                    },
                                ]"
                              >
                                <CardHeader>
                                    
                                    <CardTitle
                                        class="flex items-center justify-between gap-2 px-4">
                                        <div class="space-x-4">
                                            <FontAwesomeIcon
                                                :icon="faCrown"
                                                class="text-gold" /><span>{{
                                                campaign.campaignName
                                            }}</span>
                                        </div>
                                        <div
                                            class="transition-colors group-hover:text-primary">
                                            <FontAwesomeIcon
                                                :icon="faChevronCircleRight" />
                                        </div>
                                    </CardTitle>
                                </CardHeader>
                            </Card>
                        </NuxtLink>
                    </li>
                </ul>
            </template>
            <template
                v-if="(userStore.state.user?.memberCampaigns ?? []).length > 0">
                <header class="font-NovaCut text-xl text-gold sm:text-2xl">
                    Joined Campaigns
                </header>
                <ul>
                    <li v-for="campaign in userStore.campaignList">
                        <NuxtLink
                            :to="`/app/campaigns/${campaign.campaignId}`">
                            <Card
                                :class="[
                                    'interactable group shadow-solid-sm transition-colors hover:border-primary',
                                    {
                                        'border-destructive':
                                            campaign.currentCombatName != null,
                                    },
                                ]">
                                <CardHeader>
                                    <CardTitle
                                        class="flex items-center justify-between gap-2 px-4">
                                        <div class="space-x-4">
                                            <FontAwesomeIcon
                                                :icon="faPerson"
                                                class="text-gold" /><span>{{
                                                campaign.campaignName
                                            }}</span>
                                        </div>
                                        <div
                                            class="transition-colors group-hover:text-primary">
                                            <FontAwesomeIcon
                                                :icon="faChevronCircleRight" />
                                        </div>
                                    </CardTitle>
                                </CardHeader>
                            </Card>
                        </NuxtLink>
                    </li>
                </ul>
            </template>
        </main>
    </div>
</template>
<script setup lang="ts">
    import {
        faChevronCircleRight,
        faCrown,
        faPerson,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const userStore = useUserStore();
    definePageMeta({
        layout: "app",
        requiresAuth: true,
    });
</script>
