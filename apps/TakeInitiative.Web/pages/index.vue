<template>
    <div
        v-if="!pending && !error"
        class="flex h-full w-full flex-col overflow-auto"
    >
        <header>
            <div
                class="flex cursor-pointer items-center border-b-2 border-take-yellow bg-take-navy-medium px-4 py-1"
            >
                <h1
                    class="flex items-center gap-2 font-NovaCut text-2xl font-bold text-take-yellow sm:text-3xl md:text-4xl"
                >
                    <img
                        class="h-[1.5em] w-[1.5em]"
                        src="~assets/yellowDice.png"
                    />
                    Take Initiative
                </h1>
                <section class="flex flex-1 justify-end gap-2">
                    <div
                        class="flex items-center px-2 py-1 font-NovaCut text-xl text-take-yellow"
                    >
                        <label>{{ userStore.username }}</label>
                    </div>
                    <div class="flex items-center">
                        <FormButton
                            icon="share-from-square"
                            textColour="white"
                            size="sm"
                            buttonColour="take-navy-light"
                            hoverButtonColour="take-yellow"
                            class="h-3/4 w-3/4"
                        />
                    </div>
                    <div class="flex items-center">
                        <FormButton
                            icon="user-gear"
                            size="sm"
                            textColour="white"
                            buttonColour="take-navy-light"
                            hoverButtonColour="take-yellow"
                            class="h-3/4 w-3/4"
                        />
                    </div>
                </section>
            </div>
        </header>
        <Tabs
            class="flex-1 p-4"
            backgroundColour="take-navy-medium"
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
    { watch: [state.value] },
); // Return something so that nuxt does not recall this on this client

useHead({
    title: "Take Initiative",
});

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>
