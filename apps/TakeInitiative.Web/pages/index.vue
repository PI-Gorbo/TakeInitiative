<template>
    <TransitionGroup
        name="fade"
        tag="main"
        class="flex h-full flex-col items-center"
    >
        <div
            v-if="
                (!pending && !error) ||
                userStore.state.selectedCampaignId == null
            "
            class="flex w-full flex-1 flex-col overflow-auto md:w-4/5 md:max-w-[1200px] 2xl:w-full"
        >
            <header
                v-if="campaignStore.state.combatDto"
                :class="[
                    'mx-3 my-2 cursor-pointer select-none rounded-lg px-4 py-3 text-center text-xl text-take-navy',
                    campaignStore.state.combatDto.state == CombatState.Open
                        ? 'bg-take-yellow-dark'
                        : 'bg-take-red ',
                ]"
                @click="
                    async () =>
                        await navigateTo(
                            `/combat/${campaignStore.state.combatDto?.id}`,
                        )
                "
            >
                <div
                    v-if="
                        campaignStore.state.combatDto.state == CombatState.Open
                    "
                >
                    {{ openCombatText }}
                </div>
                <div v-else>
                    {{ combatStartedText }}
                </div>
            </header>
            <Tabs
                class="flex-1 flex-col overflow-auto"
                backgroundColour="take-navy"
                notSelectedTabColour="take-navy"
                :renameTabs="{
                    PlannedCombats: 'Planned Combats',
                    CombatHistory: 'Combat History',
                }"
                :showTabs="{
                    PlannedCombats: () => campaignStore.isDm,
                    Settings: () => campaignStore.isDm,
                    CombatHistory: () =>
                        (campaignStore.state.finishedCombats?.length ?? 0) > 0,
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
                <template #CombatHistory>
                    <IndexCombatHistorySection />
                </template>
                <template #Settings> Dungeon Master Settings </template>
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

const userStore = useUserStore();
const { state } = storeToRefs(userStore);
const campaignStore = useCampaignStore();
const { refresh, pending, error } = await useAsyncData(
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
</script>
