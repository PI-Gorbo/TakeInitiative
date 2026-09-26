<template>
    <div class="flex h-full w-full justify-center">
        <main class="w-page flex flex-col gap-4">
            <template
                v-if="dmCampaigns.length > 0">
                <header class="flex justify-between">
                    <span class="font-NovaCut text-xl text-gold sm:text-2xl"
                        >My Campaigns</span
                    >
                    <div class="flex gap-2">
                        <Sheet v-model:open="joinCampaignSheetOpen">
                            <SheetTrigger asChild>
                                <Button
                                    variant="outline"
                                    class="interactable">
                                    <FontAwesomeIcon :icon="faRightToBracket" />
                                    Join
                                </Button>
                            </SheetTrigger>
                            <SheetContent class="flex flex-col gap-4">
                                <SheetHeader>
                                    <SheetTitle> Join a campaign</SheetTitle>
                                </SheetHeader>
                                <CampaignJoinForm :submit="joinCampaign" />
                            </SheetContent>
                        </Sheet>
                        <Sheet v-model:open="createCampaignSheetOpen">
                            <SheetTrigger asChild>
                                <Button
                                    variant="outline"
                                    class="interactable">
                                    <FontAwesomeIcon :icon="faPlusCircle" />
                                    New
                                </Button>
                            </SheetTrigger>
                            <SheetContent class="flex flex-col gap-4">
                                <SheetHeader>
                                    <SheetTitle>
                                        Create a new campaign
                                    </SheetTitle>
                                </SheetHeader>
                                <CampaignCreateForm :submit="createCampaign" />
                            </SheetContent>
                        </Sheet>
                    </div>
                </header>
                <ul class="flex flex-col gap-4">
                    <li v-for="campaign in dmCampaigns" :key="campaign.id">
                        <CampaignCard
                            :campaignId="campaign.id"
                            :campaignName="campaign.name" />
                    </li>
                </ul>
            </template>
            <template
                v-if="playerCampaigns.length > 0">
                <header class="font-NovaCut text-xl text-gold sm:text-2xl">
                    Joined Campaigns
                </header>
                <ul>
                    <li v-for="campaign in playerCampaigns" :key="campaign.id">
                        <CampaignCard
                            :campaignId="campaign.id"
                            :campaignName="campaign.name" />
                    </li>
                </ul>
            </template>
        </main>
    </div>
</template>
<script setup lang="ts">
    import {
        faChevronCircleRight,
        faRightToBracket,
        faPerson,
        faPlusCircle,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { helpers } from "@typed-router";
    import type { CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
    import type { JoinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";
    import CampaignCard from "../../../components/Campaign/CampaignCard.vue";

    const userStore = useUserStore();
    const dmCampaigns = computed(() =>
        userStore.campaignList.filter((c) => c.role === "DM")
    );
    const playerCampaigns = computed(() =>
        userStore.campaignList.filter((c) => c.role === "Player")
    );
    definePageMeta({
        layout: "main-app",
        requiresAuth: true,
        middleware: [
            async () => {
                const userStore = useUserStore();
                if ((await userStore.fetchCampaigns()).length === 0) {
                    return navigateTo(helpers.path("/createOrJoinCampaign"));
                }
            },
        ],
    });

    // Create campaign
    const createCampaignSheetOpen = ref(false);

    async function createCampaign(request: CreateCampaignRequest) {
        await userStore
            .createCampaign(request)
            .then(() => (createCampaignSheetOpen.value = false));
    }

    // Join Campaign
    const joinCampaignSheetOpen = ref(false);

    async function joinCampaign(request: JoinCampaignRequest) {
        await userStore
            .joinCampaign(request)
            .then(() => (joinCampaignSheetOpen.value = false));
    }
</script>
