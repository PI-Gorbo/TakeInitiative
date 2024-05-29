<template>
    <div class="h-full w-full">
        <NuxtLayout name="default">
            <div v-if="pending" class="w-full h-full flex justify-center items-center text-9xl">
                <FontAwesomeIcon icon="spinner" class="fa-spin"/>
            </div>
            <div v-else-if="error" class="w-full h-full flex text-center text-2xl">
                Something has gone wrong. Please reload the page.
            </div>
            <div v-else class="flex h-full flex-col items-center">
                <header class="flex gap-2 py-2 md:w-4/5 md:max-w-[1200px]">
                    <div
                        v-for="tab in visibleTabs"
                        :key="tab.name"
                        :class="[
                            selectedTab == tab.name
                                ? `bg-take-yellow text-take-navy`
                                : `bg-take-navy text-take-navy-light hover:bg-take-navy-light hover:text-white`, // PLEASE CHANGE TO MANUALLY CONTROLLED TEXT COLOUR
                            `flex cursor-pointer select-none items-center rounded-md px-2 py-1 font-NovaCut transition-colors   md:text-lg`,
                        ]"
                        @click="() => navigateToSelectedTab(tab.name)"
                    >
                        <div class="capitalize">{{ tab.name }}</div>
                    </div>
                </header>
                <main class="w-full flex-1 md:w-4/5 md:max-w-[1200px]">
                    <slot />
                </main>
            </div>
        </NuxtLayout>
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome';

// Fetch and Set current campaign.
const route = useRoute();
const campaignStore = useCampaignStore();
const { isDm } = storeToRefs(campaignStore);
const { pending, error } = await useAsyncData(
    "campaignPages",
    async () => {
        const wait = (t: number) =>
            new Promise((resolve, reject) => setTimeout(resolve, t));
        await wait(2000);

        if (!route.name?.toString().startsWith("campaign-id")) {
            throw Error("not on the right page");
        }

        const id = route.params.id as string | null;
        if (id == null) {
            throw Error("Id was not valid. Given id: " + id);
        }

        return await campaignStore
            .setCampaignById(route.params.id as string)
            .then(() => true);
    },
    {
        watch: [() => route.params.id],
    },
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
