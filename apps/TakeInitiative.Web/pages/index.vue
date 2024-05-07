<template>
    <TransitionGroup
        name="fade"
        tag="main"
        class="flex h-full flex-col items-center overflow-y-auto bg-take-navy text-white"
    >
        <div
            v-if="
                (!pending && !error) ||
                userStore.state.selectedCampaignId == null
            "
            class="flex w-full flex-1 flex-col overflow-auto md:w-4/5 md:max-w-[1200px] 2xl:w-full"
        >
            <Tabs
                class="flex-1 flex-col overflow-auto"
                backgroundColour="take-navy"
                notSelectedTabColour="take-navy"
                :renameTabs="{
                    Combats: 'Combats',
                }"
                :showTabs="{
                    Combats: () => campaignStore.isDm,
                    Settings: () => campaignStore.isDm,
                }"
                negativeSectionId="IndexPageTabs"
            >
                <template #Summary>
                    <IndexSummarySection />
                </template>
                <template #Characters> Characters... </template>
                <template #Combats>
                    <IndexCombatSection />
                </template>
                <template #Settings>
                    <main class="flex w-2/3 flex-col gap-4">
                        <div class="w-fit">
                            <FormToggleableInput
                                label="Campaign Name"
                                v-model:value="campaignName"
                                buttonColour="take-navy-medium"
                                notEditableColour="take-navy-medium"
                                :onSave="
                                    async () => {
                                        return userStore.updateCampaign({
                                            campaignId: campaign?.campaignId!,
                                            campaignName: campaignName,
                                        });
                                    }
                                "
                            />
                        </div>
                        <div>
                            <FormButton
                                label="Delete Campaign"
                                icon="trash"
                                buttonColour="take-navy-light"
                                hoverButtonColour="take-red"
                                size="sm"
                                :click="() => deleteCampaign()"
                            />
                        </div>
                    </main>
                </template>
            </Tabs>
        </div>
        <div v-else class="flex h-full w-full items-center justify-center">
            <FontAwesomeIcon class="fa-spin" icon="circle-notch" size="10x" />
        </div>
    </TransitionGroup>
</template>

<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";
import { CombatState } from "~/utils/types/models";

const campaignName = ref<string | undefined>(undefined);
const userStore = useUserStore();
const { selectedCampaignDto: campaign } = storeToRefs(userStore);
const campaignStore = useCampaignStore();
const { refresh, pending, error } = await useAsyncData(
    "Campaign",
    () => {
        return campaignStore
            .init()
            .then(
                () =>
                    (campaignName.value =
                        campaignStore.state.campaign?.campaignName!),
            )
            .then(() => true);
    },
    { watch: [() => userStore.state.selectedCampaignId] },
); // Return something so that nuxt does not recall this on this client
onMounted(
    () => (campaignName.value = campaignStore.state.campaign?.campaignName!),
);
useHead({
    title: "Take Initiative",
});

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
    layout: "index-page",
});

const openCombatText = computed(() => {
    const combatDto = campaignStore.state.combatDto;
    const combatOpenedByUser = campaignStore.state.nonUserCampaignMembers
        ?.concat([
            {
                userId: userStore.state.user?.userId!,
                username: userStore.username!,
            } satisfies {
                userId: string;
                username: string;
            },
        ])
        .find((x) => x.userId == combatDto?.dungeonMaster)?.username;

    return `The combat '${combatDto?.combatName}' has been opened by ${combatOpenedByUser}! Click to join or start watching before it starts...`;
});
const combatStartedText = computed(() => {
    const combatDto = campaignStore.state.combatDto;
    const combatOpenedByUser = campaignStore.state.nonUserCampaignMembers
        ?.concat([
            {
                userId: userStore.state.user?.userId!,
                username: userStore.username!,
            } satisfies {
                userId: string;
                username: string;
            },
        ])
        .find((x) => x.userId == combatDto?.dungeonMaster)?.username;

    return `The combat '${combatDto?.combatName}' has started! Click to join or watch.`;
});

function deleteCampaign() {
    console.log(campaignStore.state.campaign?.id);
    return userStore
        .deleteCampaign({ campaignId: campaignStore.state.campaign?.id! })
        .then(async () => {
            if (userStore.campaignCount == 0) {
                await navigateTo("/createOrJoinCampaign");
            } else {
                userStore.setSelectedCampaign(
                    userStore.campaignList![0].campaignId,
                );
            }
        });
}
</script>
