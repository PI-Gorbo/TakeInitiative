<template>
    <TransitionGroup name="fade" class="h-full w-full">
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
            v-if="plannedCombats.length != 0 || combatHistory.length != 0"
        >
            <aside
                key="combatAside"
                v-if="
                    !(
                        isSmallScreen &&
                        (selectedPlannedCombat || selectedFinishedCombat)
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
                    <div>
                        <FontAwesomeIcon icon="pen-to-square" />
                        <span class="font-NovaCut text-take-yellow">
                            Planned Combats
                        </span>
                    </div>
                    <ul class="flex flex-col gap-2 overflow-y-auto">
                        <li
                            v-for="plannedCombat in plannedCombats"
                            :key="plannedCombat.id"
                            :class="[
                                'flex select-none items-center justify-between rounded-md border border-take-navy-dark bg-take-navy-dark p-2',
                                selectedPlannedCombat?.id == plannedCombat.id &&
                                    'border-take-yellow',
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
                                            selectedFinishedCombat = null;
                                            campaignStore.setPlannedCombat(
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
                            v-for="finishedCombat in combatHistory"
                            :key="finishedCombat.combatId"
                            class="flex select-none items-center justify-between rounded-md bg-take-navy-dark p-2"
                        >
                            <span class="px-1">{{ finishedCombat.name }}</span>
                            <FormButton
                                icon="eye"
                                label="View"
                                buttonColour="take-navy-dark"
                                @clicked="
                                    () => {
                                        selectedFinishedCombat =
                                            finishedCombat.combatId;
                                        campaignStore.setPlannedCombat(null);
                                    }
                                "
                            />
                        </li>
                    </ul>
                    <label
                        v-if="combatHistory.length == 0"
                        class="text-sm italic"
                        >Plan, Start and Finish your first combat start a
                        History!</label
                    >
                </div>
            </aside>

            <main
                key="combatMain"
                v-if="selectedPlannedCombat"
                class="col-span-7 flex flex-col overflow-y-auto"
            >
                <header class="flex items-center gap-2 pb-2">
                    <FormButton
                        v-if="isSmallScreen"
                        icon="arrow-left"
                        buttonColour="take-navy"
                        @click="() => campaignStore.setPlannedCombat(null)"
                    />
                    <div :class="['px-2 text-center font-NovaCut']">
                        {{ selectedPlannedCombat.combatName }}
                    </div>
                </header>
                <div class="overflow-y-auto">
                    <IndexPlannedCombatSection />
                </div>
            </main>

            <main
                key="combatMain"
                v-if="selectedFinishedCombat"
                class="col-span-7"
            >
                <FormButton
                    v-if="isSmallScreen"
                    icon="arrow-left"
                    size="lg"
                    buttonColour="take-navy"
                    @click="() => (selectedFinishedCombat = null)"
                />
                Finished combat summary coming soon...
            </main>
        </TransitionGroup>

        <section
            class="flex flex-col items-center px-2"
            v-else
            key="createFirstPlannedCombat"
        >
            <h2 class="w-full text-center text-xl">
                Create your first planned combat
            </h2>
            <CreatePlannedCombatForm
                :onCreatePlannedCombat="onCreatePlannedCombat"
            />
        </section>

        <Modal
            ref="createPlannedCombatModal"
            title="Create a planned combat"
            key="CreatePlannedCombatModal"
        >
            <CreatePlannedCombatForm
                class="h-full w-full"
                :onCreatePlannedCombat="onCreatePlannedCombat"
            />
        </Modal>

        <ConfirmModal
            key="ConfirmModal"
            ref="deleteCombatModal"
            confirmText="Delete"
            confirmColour="take-red"
            :closeOnConfirm="false"
            @Confirm="(ctrls) => deleteCombat(ctrls, selectedPlannedCombat)"
            cancelText="Cancel"
            cancelColour="take-yellow"
            bodyText="Delete this planned combat?"
        />
    </TransitionGroup>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import Modal from "~/components/Modal.vue";
import ConfirmModal from "~/components/ConfirmModal.vue";
import type { ButtonLoadingControl } from "../Form/Button.vue";
import type { PlannedCombat } from "~/utils/types/models";
import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";

// Screen Size
const isSmallScreen = computed(() => {
    return window.matchMedia("(max-width: 640px)").matches;
});

// Modals
const deleteCombatModal = ref<InstanceType<typeof ConfirmModal> | null>(null);
const createPlannedCombatModal = ref<InstanceType<typeof Modal> | null>(null);

const campaignStore = useCampaignStore();
const hasActiveCombat = computed(() => campaignStore.state.combatDto != null);
const plannedCombatStore = usePlannedCombatStore();
const plannedCombats = computed(() => campaignStore.state.plannedCombats ?? []);
const combatHistory = computed(() => campaignStore.state.finishedCombats ?? []);

const selectedFinishedCombat = ref<string | null>(null);
const selectedPlannedCombat = computed(
    () => plannedCombatStore.selectedPlannedCombat,
);

// Create planned combat
async function showCreatePlannedCombatModal() {
    createPlannedCombatModal.value?.show();
}

async function onCreatePlannedCombat(
    input: void | Omit<CreatePlannedCombatRequest, "campaignId">,
) {
    return await campaignStore
        .createPlannedCombat(input!)
        .then((plannedCombat) => {
            return campaignStore.setPlannedCombat(plannedCombat.id);
        })
        .then(() => createPlannedCombatModal.value?.hide());
}

// Delete combat
async function showDeleteCombatModal(
    loadingCtrl: ButtonLoadingControl,
    combat: PlannedCombat,
) {
    loadingCtrl.setLoaded();
    // If the combat has no stages, or all the stages are empty, then just delete without showing the modal.
    if (
        combat.stages?.length == 0 ||
        combat.stages?.flatMap((x) => x.NPCs).filter((x) => x != null).length ==
            0
    ) {
        loadingCtrl.setLoaded();
        await deleteCombat(loadingCtrl, combat);
        return;
    }

    // Show the delete confirmation modal.
    if (deleteCombatModal.value) {
        deleteCombatModal.value?.show();
    }
}

async function deleteCombat(
    loadingCtrl: ButtonLoadingControl,
    combat: PlannedCombat,
) {
    loadingCtrl.setLoading();
    return await campaignStore.deletePlannedCombat(combat.id).then(() => {
        loadingCtrl.setLoaded();
        deleteCombatModal.value?.hide();
    });
}

async function onOpenCombat(plannedCombatId: string) {
    return await campaignStore.openCombat(plannedCombatId);
}

onMounted(() => {
    if (
        !isSmallScreen.value && // Ensure not a smalls screen
        plannedCombats.value &&
        plannedCombats.value.length > 0 &&
        selectedPlannedCombat.value == null
    ) {
        campaignStore.setPlannedCombat(plannedCombats.value[0].id);
    }
});
</script>
