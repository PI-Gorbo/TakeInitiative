<template>
    <!-- The share target's landing page (16e). The service worker kept the shared
         images; this page picks the campaign and hands them to its composer. -->
    <div class="flex flex-col gap-4">
        <h1 class="font-NovaCut text-2xl text-gold">{{ SHARE_MESSAGES.pick }}</h1>

        <LoadingFallback
            v-if="status === 'loading'"
            :isLoading="true"
            iconSize="2x"
            class="pt-8" />

        <template v-else-if="status === 'pick'">
            <p class="text-sm text-muted-foreground">
                {{ itemSummary }}
            </p>
            <ul class="flex flex-col divide-y rounded-lg border">
                <li
                    v-for="campaign in campaigns"
                    :key="campaign.id">
                    <button
                        type="button"
                        class="flex min-h-16 w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-accent/60"
                        @click="go(campaign.id)">
                        <span class="min-w-0 flex-1 truncate font-medium">{{ campaign.name }}</span>
                        <ChevronRight class="size-5 text-muted-foreground" />
                    </button>
                </li>
            </ul>
        </template>

        <div
            v-else
            role="alert"
            class="flex flex-col items-start gap-3 rounded-lg border p-4">
            <p>{{ message }}</p>
            <NuxtLink
                :to="status === 'noCampaign' ? '/createOrJoinCampaign' : '/app/campaigns'"
                class="inline-flex min-h-11 items-center text-sm text-gold underline">
                {{ status === "noCampaign" ? "Join or create a campaign" : "Go to your campaigns" }}
            </NuxtLink>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { ChevronRight } from "lucide-vue-next";
    import type { CampaignSummary } from "~/utils/api/types";
    import {
        SHARE_MESSAGES,
        SHARE_PARAM,
        browserCaches,
        chooseCampaign,
        deleteShare,
        pruneShares,
        readShare,
        rememberedCampaign,
        shareIsEmpty,
        validShareId,
        type SharedItem,
    } from "~/utils/shareTarget";

    // Signed out, the global auth check sends the user to log in with this whole URL as
    // `redirectTo`, and the share waits in Cache Storage meanwhile (for an hour).
    definePageMeta({
        layout: "app",
        requiresAuth: true,
    });

    const route = useRoute();
    const userStore = useUserStore();

    const status = ref<"loading" | "pick" | "noCampaign" | "error">("loading");
    const message = ref("");
    const campaigns = ref<CampaignSummary[]>([]);
    const item = ref<SharedItem>();
    const shareId = validShareId(route.query.id);

    const itemSummary = computed(() => {
        const count = item.value?.files.length ?? 0;
        if (count === 0) return "Choose the campaign for this note.";
        return `Choose the campaign for ${count === 1 ? "this image" : `these ${count} images`}.`;
    });

    function fail(text: string) {
        status.value = "error";
        message.value = text;
    }

    function go(campaignId: string) {
        void navigateTo(
            { path: `/app/campaigns/${campaignId}`, query: { [SHARE_PARAM]: shareId } },
            { replace: true }
        );
    }

    onMounted(async () => {
        const caches = browserCaches();
        await pruneShares(caches).catch(() => 0);

        const error = route.query.error;
        if (error === "unavailable") return fail(SHARE_MESSAGES.unavailable);
        if (error) return fail(SHARE_MESSAGES.failed);
        if (!shareId) return fail(SHARE_MESSAGES.missing);

        const shared = await readShare(caches, shareId).catch(() => undefined);
        if (!shared) return fail(SHARE_MESSAGES.missing);
        if (shareIsEmpty(shared)) {
            await deleteShare(caches, shareId).catch(() => undefined);
            return fail(shared.notImages > 0 ? SHARE_MESSAGES.notImages : SHARE_MESSAGES.missing);
        }
        item.value = shared;

        let storage: Storage | undefined;
        try {
            storage = window.localStorage;
        } catch {
            storage = undefined;
        }
        const remembered = rememberedCampaign(storage);
        let list: CampaignSummary[];
        try {
            list = await userStore.fetchCampaigns();
        } catch {
            return fail("Your campaigns could not be loaded. Share again.");
        }
        const choice = chooseCampaign(list, remembered);
        if (choice.kind === "go") return go(choice.campaignId);
        if (choice.kind === "none") {
            // The share stays in the cache for an hour, so it can be shared again after joining.
            status.value = "noCampaign";
            message.value = SHARE_MESSAGES.noCampaign;
            return;
        }
        campaigns.value = choice.campaigns;
        status.value = "pick";
    });
</script>
