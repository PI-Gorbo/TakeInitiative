<template>
    <div v-if="!pending && !error" class="flex h-full w-full flex-col overflow-auto">
        <Tabs
            class="flex-1 py-4"
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
    </div>
</template>

<script setup lang="ts">
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
    { watch: [state.value] }
); // Return something so that nuxt does not recall this on this client

useHead({
    title: "Take Initiative",
});

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>
