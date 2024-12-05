<template>
    <TransitionGroup name="fade" class="h-full w-full p-2" tag="main">
        <div key="loading" v-if="pending || campaignStore.state == undefined">
            Loading...
        </div>

        <TransitionGroup
            name="fade"
            tag="section"
            key="combatMainDisplay"
            :class="[
                'h-full w-full grid-cols-10 overflow-y-auto',
                {
                    '': isSmallScreen,
                    grid: !isSmallScreen,
                },
            ]"
            v-else-if="
                campaignCombatsStore.hasAnyPlannedCombats ||
                campaignCombatsStore.hasAnyCombats ||
                hasActiveCombat
            ">
            <aside
                key="combatAside"
                v-if="
                    !(
                        isSmallScreen &&
                        campaignCombatsStore.state?.selectedCombat
                    )
                "
                :class="[
                    'flex flex-col gap-8',
                    {
                        'col-span-3': !isSmallScreen,
                    },
                ]">
                <div class="flex flex-col gap-2" v-if="hasActiveCombat">
                    <div class="flex items-center justify-between">
                        <div>
                            <FontAwesomeIcon icon="hand-fist" />
                            <span class="font-NovaCut text-take-yellow">
                                Active Combat
                            </span>
                        </div>
                    </div>
                    <IndexCombatBanner />
                </div>

                <div class="flex flex-col gap-2">
                    <div class="flex items-center justify-between">
                        <div>
                            <FontAwesomeIcon icon="pen-to-square" />
                            <span class="font-NovaCut text-lg text-take-yellow">
                                Planned Combats
                            </span>
                        </div>
                        <FormButton
                            label="New"
                            icon="plus"
                            size="sm"
                            buttonColour="take-navy"
                            @clicked="showCreatePlannedCombatModal" />
                    </div>
                    <ul class="flex flex-col gap-2 overflow-y-auto">
                        <li
                            v-for="plannedCombat in campaignCombatsStore.state
                                ?.plannedCombats ?? []"
                            :key="plannedCombat.id"
                            :class="[
                                'flex select-none items-center justify-between rounded-md border border-take-purple bg-take-purple p-1',
                                campaignCombatsStore.state.selectedCombat?.id ==
                                    plannedCombat.id && 'border-take-yellow',
                            ]">
                            <span class="px-1">{{
                                plannedCombat.combatName
                            }}</span>
                            <div class="flex gap-1">
                                <FormButton
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
                                    " />
                            </div>
                        </li>
                    </ul>
                </div>

                <div class="flex flex-col gap-2">
                    <div>
                        <FontAwesomeIcon icon="flag-checkered" />
                        <span class="font-NovaCut text-lg text-take-yellow">
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
                                    campaignCombatsStore.selectedCombat
                                        ?.combatId && 'border-take-yellow',
                            ]">
                            <span class="px-1">{{
                                finishedCombat.combatName
                            }}</span>
                            <FormButton
                                icon="eye"
                                label="View"
                                buttonColour="take-purple-light"
                                @clicked="
                                    () => {
                                        campaignCombatsStore.selectCombat(
                                            finishedCombat.combatId
                                        );
                                    }
                                " />
                        </li>
                    </ul>
                    <label
                        v-if="campaignCombatsStore.state?.combats?.length == 0"
                        class="text-sm italic"
                        >Plan, Start and Finish your first combat start a
                        History!</label
                    >
                </div>
            </aside>

            <main
                key="plannedCombat"
                v-if="campaignCombatsStore.selectedPlannedCombat"
                class="col-span-7 flex flex-col overflow-y-auto">
                <header class="flex items-center justify-between gap-2 pb-2">
                    <FormButton
                        v-if="isSmallScreen"
                        icon="arrow-left"
                        buttonColour="take-navy"
                        @click="() => campaignCombatsStore.unselectCombat()" />
                    <div :class="['px-2 text-center font-NovaCut']">
                        {{
                            campaignCombatsStore.selectedPlannedCombat
                                .combatName
                        }}
                    </div>
                    <FormButton
                        icon="trash"
                        label="Delete Combat"
                        buttonColour="take-purple-light"
                        hoverButtonColour="take-red"
                        :click="
                            () =>
                                campaignCombatsStore.deletePlannedCombat(
                                    campaignCombatsStore.selectedPlannedCombat
                                        ?.id!
                                )
                        " />
                </header>
                <div class="overflow-y-auto">
                    <IndexPlannedCombatSection />
                </div>
            </main>

            <main
                key="combat"
                v-else-if="campaignCombatsStore.selectedCombat"
                class="col-span-7">
                <FormButton
                    v-if="isSmallScreen"
                    icon="arrow-left"
                    size="lg"
                    buttonColour="take-navy"
                    @click="() => campaignCombatsStore.unselectCombat()" />

                <CombatHistorySection
                    :combatId="campaignCombatsStore.selectedCombat.combatId" />
            </main>

            <main
                key="no-combat"
                v-else-if="
                    !isSmallScreen &&
                    !campaignCombatsStore.selectedCombat &&
                    !campaignCombatsStore.selectedPlannedCombat
                "
                class="col-span-7 p-2 px-4">
                <div
                    class="flex h-full w-full cursor-pointer items-center justify-center rounded border border-dashed border-take-purple-light"
                    @click="showCreatePlannedCombatModal">
                    Create a planned combat or finish the current combat to see
                    something here
                </div>
            </main>
        </TransitionGroup>

        <section
            class="flex justify-center"
            v-else
            key="createFirstPlannedCombat">
            <div
                class="flex w-full flex-col items-center rounded bg-take-purple-dark px-5 py-2 md:w-2/3">
                <h2 class="w-full text-center text-lg">
                    Plan your first Combat
                </h2>
                <p class="w-full text-center text-sm">
                    Add NPCs now, and then start the combat when you are ready!
                </p>
                <CreatePlannedCombatForm
                    class="w-full"
                    :onCreatePlannedCombat="
                        (req, create) => onCreatePlannedCombat(req!, create)
                    " />
            </div>
        </section>

        <Modal
            ref="createPlannedCombatModal"
            title="Create a planned combat"
            key="CreatePlannedCombatModal"
            x>
            <CreatePlannedCombatForm
                class="h-full w-full"
                :onCreatePlannedCombat="
                    (req, create) => onCreatePlannedCombat(req!, create)
                " />
        </Modal>
    </TransitionGroup>
</template>
<script setup lang="ts">
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import Modal from "base/components/Modal.vue";
    import ConfirmModal from "base/components/ConfirmModal.vue";
    import type { CreatePlannedCombatRequest } from "base/utils/api/plannedCombat/createPlannedCombatRequest";
    import {
        getCombatsRequest,
        type GetCombatsResponse,
    } from "base/utils/api/combat/getCombatsRequest";
    import type { CombatDto } from "base/utils/api/campaign/getCampaignRequest";

    // Page info
    definePageMeta({
        requiresAuth: true,
        layout: "campaign-tabs",
    });

    // Screen Size
    const { isMobile } = useDevice();
    const isSmallScreen = computed(() => {
        return isMobile || window?.matchMedia("(max-width: 640px)").matches;
    });

    // Stores
    const route = useRoute();
    const campaignCombatsStore = useCampaignCombatsStore();
    const campaignStore = useCampaignStore();
    const hasActiveCombat = computed(
        () => campaignStore.state.currentCombatInfo != null
    );

    // Page Setup
    const { pending, error } = await useAsyncData(
        "CampaignCombats",
        async (nuxtApp) => {
            const getData = getCombatsRequest(nuxtApp?.$axios!);
            return await getData({
                campaignId: route.params.id as string,
            })
                .then((resp) => {
                    campaignCombatsStore.state.campaignId = route.params
                        .id as string;
                    Object.keys(resp).forEach((key) => {
                        // @ts-ignore
                        campaignCombatsStore.state[key] = resp[key];
                    });
                })
                .then(selectCombatIfNoneSelected);
        },
        {
            watch: [() => route.params.id],
        }
    );

    // Modals
    const deleteCombatModal = ref<InstanceType<typeof ConfirmModal> | null>(
        null
    );
    const createPlannedCombatModal = ref<InstanceType<typeof Modal> | null>(
        null
    );

    // Create planned combat
    async function showCreatePlannedCombatModal() {
        createPlannedCombatModal.value?.show();
    }

    async function onCreatePlannedCombat(
        input: Omit<CreatePlannedCombatRequest, "campaignId">,
        startCombatImmediately: boolean = false
    ) {
        return await campaignCombatsStore
            .createPlannedCombat(input)
            .then(async (pc) => {
                createPlannedCombatModal.value?.hide();
                if (startCombatImmediately) {
                    return await onOpenCombat(pc?.id);
                }
            });
    }

    async function onOpenCombat(plannedCombatId: string) {
        return campaignStore
            .openCombat(plannedCombatId)
            .then((c) =>
                Promise.resolve(
                    useNavigator().toCombat(
                        campaignStore.state.currentCombatInfo?.id!
                    )
                )
            );
    }

    function selectCombatIfNoneSelected() {
        if (isSmallScreen.value) {
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

    const orderedFinishedCombats = computed(() =>
        (campaignCombatsStore.state?.combats ?? [])
            .sort(sortByFinishedTimestamp)
            .reverse()
    );
</script>
