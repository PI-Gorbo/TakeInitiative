<template>
    <div class="flex h-full w-full flex-col">
        <!-- Not wrapped in the default layout: the layout transition can only animate an
             element root, not a nested <NuxtLayout>. So its Toaster is repeated below. -->
        <!-- One responsive shell: tabs sit at the bottom on a phone and move to a
             side rail on desktop. The same <nav> does both. -->
        <div class="flex h-full w-full flex-col bg-background md:flex-row">
            <div class="flex min-h-0 min-w-0 flex-1 flex-col">
                <header
                    class="sticky top-0 z-10 border-b bg-background/95 pt-safe px-safe backdrop-blur">
                    <div class="flex h-14 items-center gap-1 px-1">
                        <CampaignSwitcher
                            :campaignId="campaignId"
                            :name="campaignQuery.data.value?.name ?? ''" />
                        <button
                            type="button"
                            class="flex h-11 min-w-11 shrink-0 items-center justify-center gap-2 rounded-md px-2 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                            aria-label="Search"
                            aria-keyshortcuts="Meta+K Control+K"
                            @click="openSearch($event.currentTarget as HTMLElement)">
                            <Search class="size-5" />
                            <kbd
                                class="hidden rounded border px-1.5 text-xs md:inline"
                                >{{ shortcutLabel }}</kbd
                            >
                        </button>
                    </div>
                </header>

                <main class="min-h-0 flex-1 overflow-y-auto px-safe">
                    <LoadingFallback
                        :isLoading="campaignQuery.isLoading.value"
                        :isError="campaignQuery.isError.value"
                        iconSize="3x"
                        class="pt-8">
                        <slot />
                    </LoadingFallback>
                </main>
            </div>

            <!-- On a phone the tab bar hides while the composer is pinned above the
                 keyboard, so nothing sits between them (14e, invariant 11). -->
            <nav
                aria-label="Campaign sections"
                :class="[
                    'order-last shrink-0 border-t bg-background pb-safe px-safe md:order-first md:w-56 md:border-r md:border-t-0 md:pr-0 md:pt-safe',
                    hideTabBar && 'max-md:hidden',
                ]">
                <NuxtLink
                    to="/app/campaigns"
                    class="hidden h-14 items-center gap-2 px-4 font-NovaCut text-lg text-gold md:flex">
                    <img
                        src="/yellowDice.png"
                        alt=""
                        class="size-8" />
                    Take Initiative
                </NuxtLink>
                <ul class="grid grid-cols-3 md:flex md:flex-col md:gap-1 md:p-2">
                    <li
                        v-for="tab in tabs"
                        :key="tab.name">
                        <NuxtLink
                            :to="{ name: tab.name, params: { campaignId } }"
                            :aria-current="isCurrentTab(tab) ? 'page' : undefined"
                            :class="[
                                'flex min-h-14 flex-col items-center justify-center gap-0.5 text-xs transition-colors md:min-h-11 md:flex-row md:justify-start md:gap-3 md:rounded-md md:px-3 md:text-sm',
                                isCurrentTab(tab)
                                    ? 'text-gold md:bg-accent'
                                    : 'text-muted-foreground hover:text-foreground md:hover:bg-accent/60',
                            ]">
                            <!-- The Combat tab's icon pulses while a combat is live (18e.3). -->
                            <component
                                :is="tab.icon"
                                :class="[
                                    'size-6 md:size-5',
                                    tab.name === COMBAT_TAB && combatLive && 'text-gold motion-safe:animate-pulse',
                                ]" />
                            <span
                                v-if="tab.name === COMBAT_TAB && combatLive"
                                class="sr-only"
                                >(a combat is live)</span
                            >
                            {{ tab.label }}
                        </NuxtLink>
                    </li>
                </ul>
            </nav>
        </div>

        <SearchSheet
            v-model:open="searchOpen"
            :campaignId="campaignId"
            :returnFocus="returnFocus"
            @addToWiki="openAddToWiki"
            @openMembers="openMembers" />
        <!-- On every tab: the Campaign tab's Members button and ⌘K's Share open it. -->
        <CampaignMembersPanel
            v-if="campaignQuery.data.value"
            v-model:open="membersOpen"
            :campaign="campaignQuery.data.value" />
        <!-- + Wiki from a ⌘K reference row (20d); the card page has its own. -->
        <ReferenceAddToWikiDialog
            v-model:open="addToWikiOpen"
            :campaignId="campaignId"
            :item="addToWikiItem" />
        <!-- iOS raises the keyboard only for a focus made during the tap, and the sheet's
             input mounts after it: this holds the focus (and the keyboard) until then. -->
        <input
            ref="focusProxy"
            type="text"
            tabindex="-1"
            aria-hidden="true"
            class="pointer-events-none fixed left-0 top-0 size-px opacity-0 text-base" />
        <ClientOnly>
            <Toaster :position="'top-right'" :duration="1000" />
        </ClientOnly>
    </div>
</template>

<script setup lang="ts">
    import { useEventListener } from "@vueuse/core";
    import { useQuery } from "@tanstack/vue-query";
    import { BookOpen, Castle, Search, Swords } from "lucide-vue-next";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getCombatsQuery } from "~/utils/queries/combats";
    import type { SearchReferenceHit } from "~/utils/api/types";
    import { addToWikiItemFromHit, type AddToWikiItem } from "~/utils/reference";
    import { OPEN_MEMBERS, OPEN_SEARCH } from "~/utils/search";
    import { rememberCampaign, rememberRecentCampaign } from "~/utils/shareTarget";

    const route = useRoute();
    const campaignId = computed(
        () => (route.params as { campaignId?: string }).campaignId ?? ""
    );

    const campaignQuery = useQuery(
        getCampaignQuery(campaignId, () => navigateTo("/app/campaigns"))
    );

    useHead({
        title: () =>
            campaignQuery.data.value
                ? `${campaignQuery.data.value.name} · Take Initiative`
                : "Take Initiative",
    });

    // Glossary (§1): the three tabs are Campaign, Wiki and Combat.
    const tabs = [
        { name: "app-campaigns-campaignId", label: "Campaign", icon: Castle },
        { name: "app-campaigns-campaignId-wiki", label: "Wiki", icon: BookOpen },
        { name: "app-campaigns-campaignId-combat", label: "Combat", icon: Swords },
    ] as const;

    // The live combats (18e.3), which pushes keep current: the Combat tab pulses while
    // there is one. `motion-safe:` leaves it still under `prefers-reduced-motion`.
    const COMBAT_TAB = "app-campaigns-campaignId-combat";
    const liveCombatsQuery = useQuery(getCombatsQuery(campaignId, { status: ["Active"] }));
    const combatLive = computed(() => (liveCombatsQuery.data.value?.combats.length ?? 0) > 0);

    // The Wiki and Combat tabs stay current on their child pages (an entry page is
    // `app-campaigns-campaignId-wiki-entryId`). The Campaign tab matches exactly, since
    // every campaign route name starts with its own.
    const isCurrentTab = (tab: (typeof tabs)[number]) => {
        const name = String(route.name ?? "");
        return name === tab.name || (tab.name !== tabs[0].name && name.startsWith(`${tab.name}-`));
    };

    const hideTabBar = useComposerPinned();

    // The share page (16e) goes straight to the last opened campaign, and the
    // campaign switcher offers the recent ones.
    watch(
        campaignId,
        (id) => {
            try {
                rememberCampaign(window.localStorage, id);
                rememberRecentCampaign(window.localStorage, id);
            } catch {
                // No storage: the share page lists the campaigns instead.
            }
        },
        { immediate: true }
    );

    // Live updates for every tab.
    useCampaignHub(campaignId);

    // Search: the 🔍 button, or ⌘K / Ctrl+K.
    const searchOpen = ref(false);
    const returnFocus = shallowRef<HTMLElement | null>(null);
    const focusProxy = useTemplateRef<HTMLInputElement>("focusProxy");
    function openSearch(trigger: HTMLElement | null) {
        returnFocus.value = trigger;
        focusProxy.value?.focus({ preventScroll: true });
        searchOpen.value = true;
    }
    provide(OPEN_SEARCH, openSearch);

    const membersOpen = ref(false);
    const openMembers = () => (membersOpen.value = true);
    provide(OPEN_MEMBERS, openMembers);

    // + Wiki from ⌘K (20d): the sheet has closed; the dialog opens on the next tick.
    const addToWikiOpen = ref(false);
    const addToWikiItem = shallowRef<AddToWikiItem | null>(null);
    function openAddToWiki(hit: SearchReferenceHit) {
        addToWikiItem.value = addToWikiItemFromHit(hit);
        void nextTick(() => (addToWikiOpen.value = true));
    }
    const shortcutLabel = computed(() =>
        /Mac|iPhone|iPad/.test(navigator.platform) ? "⌘K" : "Ctrl K"
    );
    useEventListener(window, "keydown", (event: KeyboardEvent) => {
        // In the composer, ⌘K is its `@` picker, and the editor has taken the key.
        if (event.defaultPrevented) return;
        if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
            event.preventDefault();
            if (searchOpen.value) searchOpen.value = false;
            else {
                returnFocus.value = document.activeElement instanceof HTMLElement ? document.activeElement : null;
                searchOpen.value = true;
            }
        }
    });
</script>
