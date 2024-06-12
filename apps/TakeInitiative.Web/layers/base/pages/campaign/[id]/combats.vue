<template>
    <TransitionGroup name="fade" class="h-full w-full" tag="main">
        <div key="loading" v-if="pending || campaignStore.state == undefined">
            Loading...
        </div>

        <TransitionGroup
            name="fade"
            tag="section"
            key="combatMainDisplay"
            :class="[
                'h-full w-full grid-cols-10 overflow-y-auto p-2',
                {
                    '': isSmallScreen,
                    grid: !isSmallScreen,
                },
            ]"
            v-else-if="
                campaignCombatsStore.hasAnyPlannedCombats ||
                campaignCombatsStore.hasAnyCombats
            "
        >
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
                ]"
            >
                <div class="flex flex-col gap-2">
                    <div class="flex items-center justify-between">
                        <div>
                            <FontAwesomeIcon icon="pen-to-square" />
                            <span class="font-NovaCut text-take-yellow">
                                Planned Combats
                            </span>
                        </div>
                        <FormButton
                            label="New"
                            icon="plus"
                            size="sm"
                            buttonColour="take-navy"
                            @clicked="showCreatePlannedCombatModal"
                        />
                    </div>
                    <ul class="flex flex-col gap-2 overflow-y-auto">
                        <li
                            v-for="plannedCombat in campaignCombatsStore.state
                                ?.plannedCombats ?? []"
                            :key="plannedCombat.id"
                            :class="[
                                'flex select-none items-center justify-between rounded-md border border-take-navy-dark bg-take-navy-dark p-1',
                                campaignCombatsStore.state.selectedCombat?.id ==
                                    plannedCombat.id && 'border-take-yellow',
                            ]"
                        >
                            <span class="px-1">{{
                                plannedCombat.combatName
                            }}</span>
                            <div class="flex gap-1">
                                <FormButton
                                    v-if="hasActiveCombat == false"
                                    icon="circle-play"
                                    label="Start"
                                    buttonColour="take-navy-dark"
                                    :loadingDisplay="{ showSpinner: true }"
                                    :click="
                                        () => onOpenCombat(plannedCombat.id)
                                    "
                                    size="sm"
                                />

                                <FormButton
                                    icon="pen"
                                    label="Edit"
                                    buttonColour="take-navy-dark"
                                    @clicked="
                                        () => {
                                            campaignCombatsStore.selectPlannedCombat(
                                                plannedCombat.id,
                                            );
                                        }
                                    "
                                />
                            </div>
                        </li>
                    </ul>
                </div>

                <div class="flex flex-col gap-2">
                    <div>
                        <FontAwesomeIcon icon="flag-checkered" />
                        <span class="font-NovaCut text-take-yellow">
                            Combat History
                        </span>
                    </div>
                    <ul class="flex flex-col gap-2 overflow-y-auto">
                        <li
                            v-for="finishedCombat in campaignCombatsStore.state
                                ?.combats ?? []"
                            :key="finishedCombat.combatName"
                            :class="[
                                'flex select-none items-center justify-between rounded-md border border-take-navy-dark bg-take-navy-dark p-1 transition-colors',
                                finishedCombat.combatId ==
                                    campaignCombatsStore.selectedCombat
                                        ?.combatId && 'border-take-yellow',
                            ]"
                        >
                            <span class="px-1">{{
                                finishedCombat.combatName
                            }}</span>
                            <FormButton
                                icon="eye"
                                label="View"
                                buttonColour="take-navy-dark"
                                @clicked="
                                    () => {
                                        campaignCombatsStore.selectCombat(
                                            finishedCombat.combatId,
                                        );
                                    }
                                "
                            />
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
                class="col-span-7 flex flex-col overflow-y-auto"
            >
                <header class="flex items-center justify-between gap-2 pb-2">
                    <FormButton
                        v-if="isSmallScreen"
                        icon="arrow-left"
                        buttonColour="take-navy"
                        @click="() => campaignCombatsStore.unselectCombat()"
                    />
                    <div :class="['px-2 text-center font-NovaCut']">
                        {{
                            campaignCombatsStore.selectedPlannedCombat
                                .combatName
                        }}
                    </div>
                    <FormButton
                        icon="trash"
                        buttonColour="take-navy-medium"
                        hoverButtonColour="take-red"
                        :click="
                            () =>
                                campaignCombatsStore.deletePlannedCombat(
                                    campaignCombatsStore.selectedPlannedCombat
                                        ?.id!,
                                )
                        "
                    />
                </header>
                <div class="overflow-y-auto">
                    <IndexPlannedCombatSection />
                </div>
            </main>

            <main
                key="combat"
                v-else-if="campaignCombatsStore.selectedCombat"
                class="col-span-7"
            >
                <FormButton
                    v-if="isSmallScreen"
                    icon="arrow-left"
                    size="lg"
                    buttonColour="take-navy"
                    @click="() => campaignCombatsStore.unselectCombat()"
                />
                Finished combat summary coming soon...
            </main>
        </TransitionGroup>

        <section
            class="flex flex-col items-center px-2"
            v-else
            key="createFirstPlannedCombat"
        >
            <h2 class="w-full text-center text-xl">Create planned combat</h2>
            <CreatePlannedCombatForm
                :onCreatePlannedCombat="(req) => onCreatePlannedCombat(req!)"
            />
        </section>

        <Modal
            ref="createPlannedCombatModal"
            title="Create a planned combat"
            key="CreatePlannedCombatModal"
        >
            <CreatePlannedCombatForm
                class="h-full w-full"
                :onCreatePlannedCombat="(req) => onCreatePlannedCombat(req!)"
            />
        </Modal>
    </TransitionGroup>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import Modal from "base/components/Modal.vue";
import ConfirmModal from "base/components/ConfirmModal.vue";
import type { ButtonLoadingControl } from "base/components/Form/Button.vue";
import type { PlannedCombat } from "../../../utils/types/models";
import type { CreatePlannedCombatRequest } from "../../../utils/api/plannedCombat/createPlannedCombatRequest";
import { getCombatsRequest } from "../../../utils/api/combat/getCombatsRequest";

// Page info
definePageMeta({
    requiresAuth: true,
    layout: "campaign-tabs",
});

// Stores
const route = useRoute();
const campaignCombatsStore = useCampaignCombatsStore();
const campaignStore = useCampaignStore();
const hasActiveCombat = computed(
    () => campaignStore.state.currentCombatInfo != null,
);

// Page Setup
const { pending, error } = await useAsyncData(
    "CampaignCombats",
    async (nuxtApp) => {
        const getData = getCombatsRequest(nuxtApp?.$axios!);
        return await getData({
            campaignId: route.params.id as string,
        }).then((resp) => {
            campaignCombatsStore.state.campaignId = route.params.id as string;
            Object.keys(resp).forEach((key) => {
                // @ts-ignore
                campaignCombatsStore.state[key] = resp[key];
            });
        });
    },
    {
        watch: [() => route.params.id],
    },
);

// Screen Size
const { isMobile } = useDevice();
const isSmallScreen = computed(() => {
    return isMobile || window?.matchMedia("(max-width: 640px)").matches;
});

// Modals
const deleteCombatModal = ref<InstanceType<typeof ConfirmModal> | null>(null);
const createPlannedCombatModal = ref<InstanceType<typeof Modal> | null>(null);

// Create planned combat
async function showCreatePlannedCombatModal() {
    createPlannedCombatModal.value?.show();
}

async function onCreatePlannedCombat(
    input: Omit<CreatePlannedCombatRequest, "campaignId">,
) {
    return await campaignCombatsStore
        .createPlannedCombat(input)
        .then(() => createPlannedCombatModal.value?.hide());
}

async function onOpenCombat(plannedCombatId: string) {
    return await campaignStore.openCombat(plannedCombatId).then(async () => {
        await useNavigator().toCampaignTab(
            route.params.id as string,
            "summary",
        );
    });
}

onMounted(() => {
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
            campaignCombatsStore.state.plannedCombats[0].id,
        );
    } else if (
        campaignCombatsStore.hasAnyCombats &&
        campaignCombatsStore.state.combats
    ) {
        campaignCombatsStore.selectCombat(
            campaignCombatsStore.state.combats[0].combatId,
        );
    }
});
</script>
