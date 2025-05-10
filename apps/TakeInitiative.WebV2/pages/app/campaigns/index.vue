<template>
  <div class="flex h-full w-full justify-center">
    <main class="w-page flex flex-col gap-4">
      <template
          v-if="(userStore.state.user?.dmCampaigns ?? []).length > 0">
        <header class="flex justify-between">
                    <span class="font-NovaCut text-xl text-gold sm:text-2xl"
                    >My Campaigns</span
                    >
          <div class="flex gap-2">
            <Sheet v-model:open="joinCampaignSheetOpen">
              <SheetTrigger asChild>
                <Button variant="outline" class="interactable">
                  <FontAwesomeIcon :icon="faRightToBracket"/>
                  Join
                </Button>
              </SheetTrigger>
              <SheetContent class="flex flex-col gap-4">
                <SheetHeader>
                  <SheetTitle> Join a campaign</SheetTitle>
                </SheetHeader>
                <CampaignJoinForm :submit="joinCampaign"/>
              </SheetContent>
            </Sheet>
            <Sheet v-model:open="createCampaignSheetOpen">
              <SheetTrigger asChild>
                <Button variant="outline" class="interactable">
                  <FontAwesomeIcon :icon="faPlusCircle"/>
                  New
                </Button>
              </SheetTrigger>
              <SheetContent class="flex flex-col gap-4">
                <SheetHeader>
                  <SheetTitle>
                    Create a new campaign
                  </SheetTitle>
                </SheetHeader>
                <CampaignCreateForm :submit="createCampaign"/>
              </SheetContent>
            </Sheet>
          </div>
        </header>
        <ul class="flex flex-col gap-4">
          <li v-for="campaign in userStore.campaignList">
            <NuxtLink :to="`/app/campaigns/${campaign.campaignId}`">
              <Card
                  v-wave
                  :class="[
                                    'interactable group transition-all',
                                ]">
                <CardHeader>
                  <CardTitle
                      class="flex items-center justify-between gap-2 px-4">
                    <div class="space-x-4 py-1">
                      <FontAwesomeIcon
                          :icon="faCrown"
                          class="text-gold"/>
                      <span>{{
                          campaign.campaignName
                        }}</span>
                    </div>
                    <div
                        v-if="campaign.currentCombatName"
                        class="rounded-md bg-gold text-gold-foreground px-2 py-1">
                      <FontAwesomeIcon
                          :icon="faHandFist"/>
                      {{ campaign.currentCombatName }}
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
                  :class="['group interactable transition-colors']">
                <CardHeader>
                  <CardTitle
                      class="flex items-center justify-between gap-2 px-4">
                    <div class="space-x-4">
                      <FontAwesomeIcon
                          :icon="faPerson"
                          class="text-gold"/>
                      <span>{{
                          campaign.campaignName
                        }}</span>
                    </div>
                    <div v-if="campaign.currentCombatName">
                      <FontAwesomeIcon
                          :icon="faHandFist"/>
                      {{ campaign.currentCombatName }}
                    </div>
                    <div
                        class="transition-colors group-hover:text-primary">
                      <FontAwesomeIcon
                          :icon="faChevronCircleRight"/>
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
  faRightToBracket,
  faPerson,
  faPlusCircle,
} from "@fortawesome/free-solid-svg-icons";
import {FontAwesomeIcon} from "@fortawesome/vue-fontawesome";
import {helpers} from "@typed-router";
import type {CreateCampaignRequest} from "~/utils/api/campaign/createCampaignRequest";
import type {JoinCampaignRequest} from "~/utils/api/campaign/joinCampaignRequest";

const userStore = useUserStore();
definePageMeta({
  layout: "main-app",
  requiresAuth: true,
  middleware: [
    () => {
      const userStore = useUserStore();
      if (
          userStore.state.user?.dmCampaigns.length === 0 &&
          userStore.state.user?.memberCampaigns.length === 0
      ) {
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
