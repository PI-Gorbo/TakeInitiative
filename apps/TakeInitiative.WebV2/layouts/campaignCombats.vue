<template>
    <div class="w-full h-full">
        <NuxtLayout name="campaign">
            <LoadingFallback
                container="main"
                :isLoading="fetchCombatHistory.status.value === 'pending'">
                <section
                    class="lg:grid w-full flex flex-col gap-4 lg:grid-cols-3 pb-2">
                    <div class="lg:col-span-1 lg:col-start-1 lg:row-start-1">
                        <Card
                            class="p-4 border-primary/50 flex flex-col gap-4 lg:block"
                            :class="{
                                hidden:
                                    campaignCombatsStore.state.selectedCombat !=
                                    null,
                            }">
                            <section>
                                <header>
                                    <FontAwesomeIcon :icon="faPenToSquare" />
                                    <span> Planned Combats </span>
                                </header>

                                <template
                                    v-if="
                                        (
                                            campaignCombatsStore.state
                                                ?.plannedCombats ?? []
                                        ).length !== 0
                                    ">
                                    <ul class="flex flex-col gap-2">
                                        <Button
                                            variant="outline"
                                            class="h-fit flex justify-between w-full items-center disabled:border-gold disabled:opacity-100"
                                            :disabled="
                                                campaignCombatsStore
                                                    .selectedPlannedCombat
                                                    ?.id === plannedCombat.id
                                            "
                                            :class="[
                                                {
                                                    interactable:
                                                        campaignCombatsStore.selectedPlannedCombat ===
                                                            undefined ||
                                                        campaignCombatsStore
                                                            .selectedPlannedCombat
                                                            .id !==
                                                            plannedCombat.id,
                                                },
                                            ]"
                                            v-for="plannedCombat in campaignCombatsStore
                                                .state?.plannedCombats ?? []"
                                            :key="plannedCombat.id"
                                            @click="
                                                () =>
                                                    campaignCombatsStore.selectPlannedCombat(
                                                        plannedCombat.id
                                                    )
                                            ">
                                            {{ plannedCombat.combatName }}
                                        </Button>
                                    </ul>
                                </template>
                                <div
                                    :class="[
                                        'flex flex-1 gap-1 items-center',
                                        (
                                            campaignCombatsStore.state
                                                ?.plannedCombats ?? []
                                        ).length === 0
                                            ? 'justify-between'
                                            : 'justify-end',
                                    ]">
                                    <span
                                        v-if="
                                            (
                                                campaignCombatsStore.state
                                                    ?.plannedCombats ?? []
                                            ).length === 0
                                        "
                                        class="text-gray-500">
                                        None yet...
                                    </span>
                                    <Button
                                        variant="link"
                                        @click="showCreatePlannedCombatModal">
                                        <FontAwesomeIcon :icon="faPlusCircle" />
                                        New
                                    </Button>
                                </div>
                            </section>

                            <section>
                                <header>
                                    <FontAwesomeIcon :icon="faFlagCheckered" />
                                    <span> Combat History </span>
                                </header>

                                <template
                                    v-if="orderedFinishedCombats.length !== 0">
                                    <ul>
                                        <Button
                                            variant="outline"
                                            class="h-fit flex justify-between w-full items-center interactable"
                                            v-for="historicalCombat in orderedFinishedCombats"
                                            :key="historicalCombat.combatId">
                                            {{ historicalCombat.combatName }}
                                        </Button>
                                    </ul>
                                </template>
                                <div
                                    :class="['flex flex-1 gap-1 items-center']">
                                    <span
                                        v-if="
                                            orderedFinishedCombats.length === 0
                                        "
                                        class="text-gray-500">
                                        None yet...
                                    </span>
                                </div>
                            </section>
                        </Card>
                    </div>
                    <div
                        class="lg:block lg:col-span-2 lg:col-start-2"
                        :class="[
                            {
                                hidden:
                                    campaignCombatsStore.state
                                        .selectedCombat === undefined,
                            },
                        ]">
                        <Card class="border-2 p-4 border-primary/50">
                            <NuxtPage />
                        </Card>
                    </div>
                </section>
            </LoadingFallback>
        </NuxtLayout>
        <Dialog v-model:open="modalState.modalOpen">
            <DialogContent class="flex flex-col gap-2">
                <DialogHeader> Create a planned combat. </DialogHeader>

                <CampaignCombatCreatePlannedCombatForm
                    :onCreatePlannedCombat="onCreatePlannedCombat" />
            </DialogContent>
        </Dialog>
    </div>
</template>
<script setup lang="ts">
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    import { type GetCombatsResponse } from "~/utils/api/combat/getCombatsRequest";
    import { useMediaQuery } from "@vueuse/core";
    import {
        faFlagCheckered,
        faPenToSquare,
        faPlusCircle,
    } from "@fortawesome/free-solid-svg-icons";
    import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";

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
    const modalState = reactive<{
        modalType: "Create-Planned-Combat";
        modalOpen: boolean;
    }>({
        modalType: "Create-Planned-Combat",
        modalOpen: false,
    });

    // Create planned combat
    async function showCreatePlannedCombatModal() {
        modalState.modalType = "Create-Planned-Combat";
        modalState.modalOpen = true;
    }

    async function onCreatePlannedCombat(
        input: Omit<CreatePlannedCombatRequest, "campaignId">,
        startCombatImmediately: boolean = false
    ) {
        return await campaignCombatsStore
            .createPlannedCombat(input)
            .then(async (pc) => {
                if (startCombatImmediately) {
                    await onOpenCombat(pc?.id);
                }
            })
            .then(() => (modalState.modalOpen = false));
    }

    async function onOpenCombat(plannedCombatId: string) {
        return campaignStore
            .openCombat(plannedCombatId)
            .then((c) =>
                Promise.resolve(
                    useNavigator().toCombat(
                        campaignStore.state.campaign?.id!,
                        campaignStore.state.currentCombatInfo?.id!
                    )
                )
            );
    }

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
