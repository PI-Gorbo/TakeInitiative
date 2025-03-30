<template>
    <div class="w-full h-full">
        <NuxtLayout name="campaign">
            <LoadingFallback
                container="main"
                :isLoading="fetchCombatHistory.status.value === 'pending'">
                <section
                    class="lg:grid w-full flex flex-col gap-4 lg:grid-cols-3 pb-2">
                    <div class="lg:col-span-1 lg:col-start-1 lg:row-start-1">
                        <Card class="p-4 border-primary/50">
                            <div class="flex flex-col gap-2">
                                <div class="flex items-center justify-between">
                                    <div>
                                        <FontAwesomeIcon
                                            :icon="faPenToSquare" />
                                        <span
                                            class="font-NovaCut text-lg text-take-yellow">
                                            Planned Combats
                                        </span>
                                    </div>
                                    <!-- <Button @click="showCreatePlannedCombatModal">
                                        <FontAwesomeIcon :icon="faPlusCircle" />
                                        New
                                    </Button> -->
                                </div>
                                <ul class="flex flex-col gap-2 overflow-y-auto">
                                    <li
                                        v-for="plannedCombat in campaignCombatsStore
                                            .state?.plannedCombats ?? []"
                                        :key="plannedCombat.id"
                                        :class="[
                                            'flex select-none items-center justify-between rounded-md border border-take-purple bg-take-purple p-1',
                                            campaignCombatsStore.state
                                                .selectedCombat?.id ==
                                                plannedCombat.id &&
                                                'border-take-yellow',
                                        ]">
                                        <span class="px-1">{{
                                            plannedCombat.combatName
                                        }}</span>
                                        <div class="flex gap-1">
                                            <!-- <FormButton
                                                v-if="hasActiveCombat == false"
                                                icon="circle-play"
                                                label="Start"
                                                buttonColour="take-purple-light"
                                                :loadingDisplay="{ showSpinner: true }"
                                                :click="
                                                    () => onOpenCombat(plannedCombat.id)
                                                "
                                                size="sm" />
                                            <FormButton
                                                icon="pen"
                                                label="Edit"
                                                buttonColour="take-purple-light"
                                                @clicked="
                                                    () => {
                                                        campaignCombatsStore.selectPlannedCombat(
                                                            plannedCombat.id
                                                        );
                                                    }
                                                " /> -->
                                        </div>
                                    </li>
                                </ul>
                            </div>
                            <div class="flex flex-col gap-2">
                                <div>
                                    <FontAwesomeIcon :icon="faFlagCheckered" />
                                    <span
                                        class="font-NovaCut text-lg text-take-yellow">
                                        Combat History
                                    </span>
                                </div>
                                <ul class="flex flex-col gap-2 overflow-y-auto">
                                    <li
                                        v-for="finishedCombat in orderedFinishedCombats"
                                        :key="finishedCombat.combatName"
                                        :class="[
                                            'flex select-none items-center justify-between rounded-md border border-take-purple bg-take-purple p-1 transition-colors',
                                            finishedCombat.combatId ==
                                                campaignCombatsStore
                                                    .selectedCombat?.combatId &&
                                                'border-take-yellow',
                                        ]">
                                        <span class="px-1">{{
                                            finishedCombat.combatName
                                        }}</span>
                                        <!-- <FormButton
                                            icon="eye"
                                            label="View"
                                            buttonColour="take-purple-light"
                                            @clicked="
                                                () => {
                                                    campaignCombatsStore.selectCombat(
                                                        finishedCombat.combatId
                                                    );
                                                }
                                            " /> -->
                                    </li>
                                </ul>
                                <label
                                    v-if="
                                        campaignCombatsStore.state?.combats
                                            ?.length == 0
                                    "
                                    class="text-sm italic"
                                    >Plan, Start and Finish your first combat
                                    start a History!</label
                                >
                            </div>
                        </Card>
                    </div>
                    <div class="lg:col-span-2 lg:col-start-2">
                        <Card
                            class="border-2 border-dashed p-4 border-primary/50">
                            Testing
                            <NuxtPage />
                        </Card>
                    </div>
                </section>
            </LoadingFallback>
        </NuxtLayout>
    </div>
</template>
<script setup lang="ts">
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    import { type GetCombatsResponse } from "~/utils/api/combat/getCombatsRequest";
    import { useMediaQuery } from "@vueuse/core";
    import {
        faFlagCheckered,
        faPenToSquare,
    } from "@fortawesome/free-solid-svg-icons";

    // Stores
    const isLargeScreen = useMediaQuery("(min-width: 1024px)");
    const route = useRoute("app-campaigns-id-combats");
    const campaignCombatsStore = useCampaignCombatsStore();
    const campaignStore = useCampaignStore();
    const hasActiveCombat = computed(
        () => campaignStore.state.currentCombatInfo != null
    );

    // Page Setup
    const fetchCombatHistory = useAsyncData(
        "CampaignCombats",
        async (nuxtApp) => {
            return campaignCombatsStore
                .init(route.params.id)
                .then(selectCombatIfNoneSelected);
        },
        {
            watch: [() => route.params.id],
        }
    );

    // Modals
    // const deleteCombatModal = ref<InstanceType<typeof ConfirmModal> | null>(
    //     null
    // );
    // const createPlannedCombatModal = ref<InstanceType<typeof Modal> | null>(
    //     null
    // );

    // // Create planned combat
    // async function showCreatePlannedCombatModal() {
    //     createPlannedCombatModal.value?.show();
    // }

    // async function onCreatePlannedCombat(
    //     input: Omit<CreatePlannedCombatRequest, "campaignId">,
    //     startCombatImmediately: boolean = false
    // ) {
    //     return await campaignCombatsStore
    //         .createPlannedCombat(input)
    //         .then(async (pc) => {
    //             createPlannedCombatModal.value?.hide();
    //             if (startCombatImmediately) {
    //                 return await onOpenCombat(pc?.id);
    //             }
    //         });
    // }

    // async function onOpenCombat(plannedCombatId: string) {
    //     return campaignStore
    //         .openCombat(plannedCombatId)
    //         .then((c) =>
    //             Promise.resolve(
    //                 useNavigator().toCombat(
    //                     campaignStore.state.currentCombatInfo?.id!
    //                 )
    //             )
    //         );
    // }

    function selectCombatIfNoneSelected() {
        if (!isLargeScreen.value) {
            return;
        }

        if (
            !campaignCombatsStore.hasAnyCombats &&
            !campaignCombatsStore.hasAnyPlannedCombats
        ) {
            return;
        }

        if (
            campaignCombatsStore.hasAnyPlannedCombats &&
            campaignCombatsStore.state.plannedCombats
        ) {
            campaignCombatsStore.selectPlannedCombat(
                campaignCombatsStore.state.plannedCombats[0].id
            );
        } else if (
            campaignCombatsStore.hasAnyCombats &&
            campaignCombatsStore.state.combats
        ) {
            campaignCombatsStore.selectCombat(
                campaignCombatsStore.state.combats
                    .sort(sortByFinishedTimestamp)
                    .reverse()[0].combatId
            );
        }
    }

    const orderedFinishedCombats = computed(() =>
        (campaignCombatsStore.state?.combats ?? [])
            .sort(sortByFinishedTimestamp)
            .reverse()
    );
    function sortByFinishedTimestamp(
        a: GetCombatsResponse["combats"][number],
        b: GetCombatsResponse["combats"][number]
    ) {
        if (a.finishedTimestamp == null && b.finishedTimestamp != null) {
            return -1;
        }

        if (a.finishedTimestamp != null && b.finishedTimestamp == null) {
            return 1;
        }

        if (a.finishedTimestamp == b.finishedTimestamp) {
            return 0;
        }

        if (a.finishedTimestamp! < b.finishedTimestamp!) {
            return -1;
        }

        if (a.finishedTimestamp! > b.finishedTimestamp!) {
            return 1;
        }

        return 0;
    }
</script>
