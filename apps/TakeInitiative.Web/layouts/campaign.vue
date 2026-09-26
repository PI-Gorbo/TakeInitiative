<template>
    <NuxtLayout name="default">
        <!-- One responsive shell: tabs sit at the bottom on a phone and move to a
             side rail on desktop. The same <nav> does both. -->
        <div class="flex h-full w-full flex-col bg-background md:flex-row">
            <div class="flex min-h-0 min-w-0 flex-1 flex-col">
                <header
                    class="sticky top-0 z-10 border-b bg-background/95 pt-safe px-safe backdrop-blur">
                    <div class="flex h-14 items-center gap-1 px-1">
                        <NuxtLink
                            to="/app/campaigns"
                            class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                            aria-label="All campaigns">
                            <ChevronLeft class="size-5" />
                        </NuxtLink>
                        <h1
                            class="min-w-0 flex-1 truncate font-NovaCut text-xl text-gold">
                            {{ campaignQuery.data.value?.name ?? "" }}
                        </h1>
                        <button
                            type="button"
                            class="flex h-11 min-w-11 shrink-0 items-center justify-center gap-2 rounded-md px-2 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                            aria-label="Search"
                            aria-keyshortcuts="Meta+K Control+K"
                            @click="searchOpen = true">
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

            <nav
                aria-label="Campaign sections"
                class="order-last shrink-0 border-t bg-background pb-safe px-safe md:order-first md:w-56 md:border-r md:border-t-0 md:pr-0 md:pt-safe">
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
                            :aria-current="route.name === tab.name ? 'page' : undefined"
                            :class="[
                                'flex min-h-14 flex-col items-center justify-center gap-0.5 text-xs transition-colors md:min-h-11 md:flex-row md:justify-start md:gap-3 md:rounded-md md:px-3 md:text-sm',
                                route.name === tab.name
                                    ? 'text-gold md:bg-accent'
                                    : 'text-muted-foreground hover:text-foreground md:hover:bg-accent/60',
                            ]">
                            <component
                                :is="tab.icon"
                                class="size-6 md:size-5" />
                            {{ tab.label }}
                        </NuxtLink>
                    </li>
                </ul>
            </nav>
        </div>

        <SearchSheet v-model:open="searchOpen" />
    </NuxtLayout>
</template>

<script setup lang="ts">
    import { useEventListener } from "@vueuse/core";
    import { useQuery } from "@tanstack/vue-query";
    import { BookOpen, Castle, ChevronLeft, Search, Swords } from "lucide-vue-next";
    import { getCampaignQuery } from "~/utils/queries/campaign";

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

    // Live updates for every tab.
    useCampaignHub(campaignId);

    // Search: the 🔍 button, or ⌘K / Ctrl+K.
    const searchOpen = ref(false);
    const shortcutLabel = computed(() =>
        /Mac|iPhone|iPad/.test(navigator.platform) ? "⌘K" : "Ctrl K"
    );
    useEventListener(window, "keydown", (event: KeyboardEvent) => {
        if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
            event.preventDefault();
            searchOpen.value = !searchOpen.value;
        }
    });
</script>
