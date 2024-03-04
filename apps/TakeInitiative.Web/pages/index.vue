<template>
    <main class="flex h-full flex-col items-center">
        <Tabs
            v-if="(!pending && !error) || userStore.state.selectedCampaignId == null"
            class="max-w-[1200px] flex-1 flex-col overflow-auto py-4 sm:w-full md:w-4/5 2xl:w-full"
            backgroundColour="take-navy"
            notSelectedTabColour="take-navy"
            :renameTabs="{
                PlannedCombats: 'Planned Combats',
            }"
            :showTabs="{
                PlannedCombats: () => {
                    return campaignStore.isDm;
                },
            }"
            negativeSectionId="IndexPageTabs"
        >
            <template #Summary>
                <IndexSummarySection />
            </template>
            <template #Characters> Characters... </template>
            <template #PlannedCombats>
                <IndexPlannedCombatsSection />
            </template>
        </Tabs>
        <div v-else class="w-full h-full flex items-center justify-center">
            <FontAwesomeIcon class="fa-spin" icon="circle-notch" size="10x" />
        </div>
    </main>
</template>

<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";

const userStore = useUserStore();
const { state } = storeToRefs(userStore);
const campaignStore = useCampaignStore();
const { refresh, pending, error } = useAsyncData(
    "Campaign",
    () => {
        return campaignStore.init().then(() => true);
    },
    { watch: [() => userStore.state.selectedCampaignId] }
); // Return something so that nuxt does not recall this on this client

useHead({
    title: "Take Initiative",
});

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>
