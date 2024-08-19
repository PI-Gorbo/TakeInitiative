<template>
    <div class="h-full w-full">
        <NuxtLayout name="default">
            <div
                v-if="pending"
                class="flex h-full w-full items-center justify-center text-9xl"
            >
                <FontAwesomeIcon icon="spinner" class="fa-spin" />
            </div>
            <div
                v-else-if="error"
                class="flex h-full w-full items-center justify-center text-2xl"
            >
                <button
                    class="h-min rounded-lg border-2 border-take-red"
                    @click="() => refresh()"
                >
                    Something has gone wrong. Click here to refresh. {{ error }}
                </button>
            </div>
            <div v-else class="flex h-full flex-col items-center">
                <header
                    class="flex flex-wrap justify-between md:w-4/5 md:max-w-[1200px]"
                >
                    <nav class="flex gap-2 py-2">
                        <div
                            v-for="tab in visibleTabs"
                            :key="tab.name"
                            :class="[
                                selectedTab == tab.name
                                    ? `bg-take-yellow text-take-navy`
                                    : `bg-take-purple-very-dark text-take-grey-dark hover:bg-take-navy-medium hover:text-take-grey-light`, // PLEASE CHANGE TO MANUALLY CONTROLLED TEXT COLOUR
                                `flex cursor-pointer select-none items-center rounded-md px-2 py-1 font-NovaCut transition-colors   md:text-lg`,
                            ]"
                            @click="() => navigateToSelectedTab(tab.name)"
                        >
                            <div class="capitalize">{{ tab.name }}</div>
                        </div>
                    </nav>
                </header>
                <main class="w-full flex-1 md:w-4/5 md:max-w-[1200px]">
                    <slot />
                </main>
            </div>
        </NuxtLayout>
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

const config = useRuntimeConfig();
// Fetch and Set current campaign.
const route = useRoute();
const campaignStore = useCampaignStore();
const { isDm } = storeToRefs(campaignStore);
const { pending, error, refresh } = await useAsyncData(
    "campaignPages",
    async () => {
        if (!route.name?.toString().startsWith("campaign-id")) {
            return false;
        }

        const id = route.params.id as string | null;
        if (id == null) {
            return false;
        }

        return await campaignStore
            .setCampaignById(route.params.id as string)
            .then(() => true);
    },
    {
        watch: [() => route.params.id],
    },
);

const {
    pending: signalRPending,
    error: signalRError,
    refresh: refreshConnection,
} = await useAsyncData(
    "campaignPages_SignalR",
    async () => {
        if (!route.name?.toString().startsWith("campaign-id")) {
            return false;
        }

        const id = route.params.id as string | null;
        if (id == null) {
            return false;
        }

        return await campaignStore.joinCampaignHub(id).then(() => true);
    },
    { server: false, watch: [() => route.params.id] },
);

type TabOption = {
    name: string;
    dmOnly: boolean;
};
const tabOptions: TabOption[] = [
    {
        name: "summary",
        dmOnly: false,
    },
    {
        name: "characters",
        dmOnly: false,
    },
    {
        name: "combats",
        dmOnly: true,
    },
    {
        name: "settings",
        dmOnly: true,
    },
];

const visibleTabs = computed(() =>
    tabOptions.filter((x) => isDm.value || x.dmOnly == false),
);
const selectedTab = computed(() => {
    switch (useRoute().name) {
        case "campaign-id-summary":
            return "summary";
        case "campaign-id-settings":
            return "settings";
        case "campaign-id-combats":
            return "combats";
        case "campaign-id-characters":
            return "characters";
        default:
            return "summary";
    }
});

function navigateToSelectedTab(routeSuffix: string) {
    return navigateTo(`/campaign/${route.params.id}/${routeSuffix}`);
}
</script>
