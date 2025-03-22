<template>
    <div class="flex h-full w-full justify-center">
        <main class="w-page flex flex-col gap-4">
            <template
                v-if="(userStore.state.user?.dmCampaigns ?? []).length > 0">
                <header class="font-NovaCut text-xl text-gold sm:text-2xl">
                    My Campaigns
                </header>
                <ul class="flex flex-col gap-4">
                    <li v-for="campaign in userStore.campaignList">
                        <NuxtLink :to="`/app/campaigns/${campaign.campaignId}`">
                            <Card
                                v-wave
                                :class="[
                                    'interactable group shadow-solid-sm transition-all hover:border-primary hover:shadow-primary',
                                ]">
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
                                            v-if="campaign.currentCombatName"
                                            class="rounded-md border-2 border-destructive p-2">
                                            <FontAwesomeIcon
                                                :icon="faHandFist"
                                                class="text-destructive" />
                                            {{ campaign.currentCombatName }}
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
                        <NuxtLink :to="`/app/campaigns/${campaign.campaignId}`">
                            <Card
                                :class="[
                                    'group interactable shadow-solid-sm hover:shadow-primary transition-colors hover:border-primary',
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
                                        <div v-if="campaign.currentCombatName">
                                            <FontAwesomeIcon
                                                :icon="faHandFist" />
                                            {{ campaign.currentCombatName }}
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
        faHandFist,
        faPerson,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { helpers } from "@typed-router";

    const userStore = useUserStore();
    definePageMeta({
        layout: "main-app",
        requiresAuth: true,
        middleware: [
            () => { 
                const userStore = useUserStore();
                if (userStore.state.user?.dmCampaigns.length === 0 && userStore.state.user?.memberCampaigns.length === 0) {
                    return navigateTo(helpers.path('/createOrJoinCampaign'))
                }
            }
        ]
    });
</script>
