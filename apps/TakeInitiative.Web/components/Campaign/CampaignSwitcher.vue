<template>
    <!-- The campaign's name opens a menu: the recently opened campaigns, then all of them. -->
    <h1 class="min-w-0 flex-1">
        <DropdownMenu @update:open="(open) => open && readRecent()">
            <DropdownMenuTrigger
                class="flex h-11 max-w-full items-center gap-1 rounded-md px-2 text-left hover:bg-accent"
                :aria-label="`${name}. Switch campaign`">
                <span class="truncate font-NovaCut text-xl text-gold">{{ name }}</span>
                <ChevronDown
                    class="size-4 shrink-0 text-muted-foreground"
                    aria-hidden="true" />
            </DropdownMenuTrigger>
            <DropdownMenuContent
                align="start"
                class="w-64">
                <template v-if="recent.length > 0">
                    <DropdownMenuLabel>Recent</DropdownMenuLabel>
                    <DropdownMenuItem
                        v-for="campaign in recent"
                        :key="campaign.id"
                        asChild
                        class="min-h-11 md:min-h-8">
                        <NuxtLink :to="`/app/campaigns/${encodeURIComponent(campaign.id)}`">
                            <span class="truncate">{{ campaign.name }}</span>
                        </NuxtLink>
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                </template>
                <DropdownMenuItem
                    asChild
                    class="min-h-11 md:min-h-8">
                    <NuxtLink to="/app/campaigns">
                        <LayoutGrid aria-hidden="true" />
                        All campaigns
                    </NuxtLink>
                </DropdownMenuItem>
            </DropdownMenuContent>
        </DropdownMenu>
    </h1>
</template>

<script setup lang="ts">
    import { ChevronDown, LayoutGrid } from "lucide-vue-next";
    import { pickRecentCampaigns, recentCampaigns } from "~/utils/shareTarget";

    const props = defineProps<{ campaignId: string; name: string }>();

    const userStore = useUserStore();
    const recentIds = ref<string[]>([]);
    function readRecent() {
        try {
            recentIds.value = recentCampaigns(window.localStorage);
        } catch {
            // No storage: the menu offers All campaigns only.
            recentIds.value = [];
        }
    }
    const recent = computed(() => pickRecentCampaigns(recentIds.value, userStore.campaignList, props.campaignId));
</script>
