<template>
    <div class="flex flex-col gap-4">
        <header class="flex items-center justify-between gap-2">
            <h1 class="font-NovaCut text-2xl text-gold">Campaigns</h1>
            <div class="flex gap-2">
                <Sheet v-model:open="joinCampaignSheetOpen">
                    <SheetTrigger asChild>
                        <Button
                            variant="outline"
                            class="h-11">
                            <LogIn /> Join
                        </Button>
                    </SheetTrigger>
                    <SheetContent class="flex flex-col gap-4 pt-safe">
                        <SheetHeader>
                            <SheetTitle>Join a campaign</SheetTitle>
                        </SheetHeader>
                        <CampaignJoinForm :submit="joinCampaign" />
                    </SheetContent>
                </Sheet>
                <Sheet v-model:open="createCampaignSheetOpen">
                    <SheetTrigger asChild>
                        <Button class="h-11"> <Plus /> New </Button>
                    </SheetTrigger>
                    <SheetContent class="flex flex-col gap-4 pt-safe">
                        <SheetHeader>
                            <SheetTitle>Create a campaign</SheetTitle>
                        </SheetHeader>
                        <CampaignCreateForm :submit="createCampaign" />
                    </SheetContent>
                </Sheet>
            </div>
        </header>

        <ul class="flex flex-col divide-y rounded-lg border">
            <li
                v-for="campaign in userStore.campaignList"
                :key="campaign.id">
                <NuxtLink
                    :to="`/app/campaigns/${campaign.id}`"
                    class="flex min-h-16 items-center gap-3 px-4 py-3 transition-colors hover:bg-accent/60">
                    <div class="flex min-w-0 flex-1 flex-col">
                        <span class="truncate font-medium">{{ campaign.name }}</span>
                        <span class="text-sm text-muted-foreground">
                            {{ campaign.memberCount }}
                            {{ campaign.memberCount === 1 ? "Member" : "Members" }}
                            <template v-if="campaign.isOwner"> · Owner</template>
                        </span>
                    </div>
                    <Badge :variant="campaign.role === 'DM' ? 'default' : 'secondary'">
                        {{ campaign.role }}
                    </Badge>
                    <ChevronRight class="size-5 text-muted-foreground" />
                </NuxtLink>
            </li>
        </ul>
    </div>
</template>

<script setup lang="ts">
    import { ChevronRight, LogIn, Plus } from "lucide-vue-next";
    import type { CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
    import type { JoinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";

    const userStore = useUserStore();

    definePageMeta({
        layout: "app",
        requiresAuth: true,
        middleware: [
            async () => {
                const userStore = useUserStore();
                if ((await userStore.fetchCampaigns()).length === 0) {
                    return navigateTo("/createOrJoinCampaign");
                }
            },
        ],
    });

    const createCampaignSheetOpen = ref(false);
    async function createCampaign(request: CreateCampaignRequest) {
        const campaign = await userStore.createCampaign(request);
        createCampaignSheetOpen.value = false;
        await useNavigator().toCampaign(campaign.id);
    }

    const joinCampaignSheetOpen = ref(false);
    async function joinCampaign(request: JoinCampaignRequest) {
        const campaign = await userStore.joinCampaign(request);
        joinCampaignSheetOpen.value = false;
        await useNavigator().toCampaign(campaign.id);
    }
</script>
