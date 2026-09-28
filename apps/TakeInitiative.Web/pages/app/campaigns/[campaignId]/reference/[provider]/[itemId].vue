<template>
    <div class="mx-auto flex w-full max-w-3xl flex-col gap-4 px-4 py-4 pb-safe">
        <!-- A reference item's stat-block card (20c), read-only, with its source's
             attribution under it. It sits in the campaign so the tabs, ⌘K and 20d's + Wiki
             have one, but the data is the same for every campaign. No tab lights up. + Wiki
             sits under the name (20d). -->
        <button
            type="button"
            class="-ml-2 flex h-11 w-fit items-center gap-1 rounded-md px-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground md:h-9"
            @click="back">
            <ChevronLeft
                class="size-4"
                aria-hidden="true" />
            Back
        </button>

        <EmptyState
            v-if="notFound"
            :icon="BookX"
            title="Not found">
            That isn't in the SRD.
            <Button
                variant="link"
                class="h-11 px-1 text-gold md:h-9"
                @click="searchAgain($event)">
                Search with {{ shortcutLabel }}
            </Button>
        </EmptyState>
        <LoadingFallback
            v-else-if="!item"
            :isLoading="itemQuery.isLoading.value"
            :isError="itemQuery.isError.value"
            iconSize="2x"
            class="pt-8" />
        <template v-else>
            <ReferenceStatBlockCard :block="item.statBlock">
                <template #actions>
                    <Button
                        variant="outline"
                        class="h-11 gap-1.5 md:h-9"
                        :aria-label="`Add ${item.summary.name} to the wiki`"
                        @click="addOpen = true">
                        <Plus
                            class="size-4"
                            aria-hidden="true" />
                        Wiki
                    </Button>
                </template>
            </ReferenceStatBlockCard>
            <ReferenceAttribution :attribution="item.attribution" />
            <ReferenceAddToWikiDialog
                v-model:open="addOpen"
                :campaignId="campaignId"
                :item="item.summary" />
        </template>
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { BookX, ChevronLeft, Plus } from "lucide-vue-next";
    import { apiErrorStatus } from "~/utils/apiErrorParser";
    import { getReferenceItemQuery } from "~/utils/queries/reference";
    import { OPEN_SEARCH } from "~/utils/search";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const route = useRoute("app-campaigns-campaignId-reference-provider-itemId");
    const router = useRouter();
    const campaignId = computed(() => route.params.campaignId);
    const provider = computed(() => route.params.provider);
    const itemId = computed(() => route.params.itemId);

    const itemQuery = useQuery(getReferenceItemQuery(provider, itemId));
    const item = computed(() => itemQuery.data.value);
    const notFound = computed(() => apiErrorStatus(itemQuery.error.value) === 404);

    // + Wiki (20d), with the item's own Stats for the dialog's line.
    const addOpen = ref(false);

    useHead({
        title: () => (item.value ? `${item.value.summary.name} · ${item.value.summary.providerLabel}` : "Reference"),
    });

    // Back to wherever the card was opened from, or the Wiki when it was opened cold.
    function back() {
        if (router.options.history.state.back) router.back();
        else void navigateTo(`/app/campaigns/${encodeURIComponent(campaignId.value)}/wiki`);
    }

    const openSearch = inject(OPEN_SEARCH, undefined);
    const shortcutLabel = computed(() =>
        /Mac|iPhone|iPad/.test(navigator.platform) ? "⌘K" : "Ctrl K"
    );
    function searchAgain(event: MouseEvent) {
        openSearch?.(event.currentTarget instanceof HTMLElement ? event.currentTarget : null);
    }
</script>
