<template>
    <main class="flex h-full flex-col">
        <header>
            <div
                class="flex select-none justify-center border-b-2 border-take-yellow bg-take-navy-medium px-4 py-1"
            >
                <div
                    class="lg:w-7/10 flex max-w-[1200px] sm:w-full md:w-4/5 2xl:w-full"
                >
                    <h1
                        @click="() => navigateTo('/')"
                        class="flex cursor-pointer items-center gap-2 font-NovaCut text-2xl font-bold text-take-yellow sm:text-3xl md:text-4xl"
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
                            />
                        </div>
                        <div class="flex items-center" @click.capture>
                            <FormButton
                                @clicked="() => navigateTo('/campaigns')"
                                icon="user-gear"
                                size="sm"
                                textColour="white"
                                buttonColour="take-navy-light"
                                hoverButtonColour="take-yellow"
                            />
                        </div>
                    </section>
                </div>
            </div>
        </header>
        <div class="flex h-full w-full flex-col items-center overflow-auto">
            <Tabs
                v-if="!pending && !error"
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
        </div>
    </main>
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
    { watch: [() => userStore.state.selectedCampaignId] },
); // Return something so that nuxt does not recall this on this client

useHead({
    title: "Take Initiative",
});

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>
