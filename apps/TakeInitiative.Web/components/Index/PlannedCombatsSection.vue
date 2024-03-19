<template>
    <TransitionGroup name="fade" class="h-full w-full">
        <section
            key="plannedCombatList"
            class="grid h-full w-full grid-cols-9"
            v-if="plannedCombats && plannedCombats.length > 0"
        >
            <aside class="col-span-3 flex flex-col gap-2 px-2 py-1">
                <header v-if="hasActiveCombat == false">
                    <FormButton
                        label="Open Combat"
                        :click="() => onOpenCombat(selectedPlannedCombat?.id!)"
                        size="sm"
                    />
                </header>
                <div
                    class="flex flex-1 flex-col gap-2 overflow-y-auto rounded-xl border border-take-navy-medium"
                >
                    <TransitionGroup
                        name="fade"
                        class="flex flex-col gap-2"
                        tag="section"
                    >
                        <div
                            v-for="combat in plannedCombats"
                            :key="combat.id"
                            :class="[
                                'group flex cursor-pointer select-none gap-4 rounded-xl border border-take-navy-medium border-opacity-100 bg-take-navy-medium p-1 transition-colors ',
                                {
                                    'border-take-yellow':
                                        combat.id == selectedPlannedCombat?.id,
                                    'hover:border-take-navy-light':
                                        combat.id != selectedPlannedCombat?.id,
                                },
                            ]"
                            @click="() => setCombat(combat)"
                        >
                            <div class="flex w-full items-center">
                                <span
                                    for="Planned Combat"
                                    class="flex flex-1 px-2 align-middle"
                                >
                                    {{ combat.combatName }}
                                </span>
                                <FormButton
                                    class="px-1"
                                    icon="trash"
                                    textColour="white"
                                    buttonColour="take-navy-light"
                                    hoverButtonColour="take-red"
                                    size="sm"
                                    @clicked="
                                        (ctrl) => showDeleteCombatModal(ctrl, combat)
                                    "
                                />
                            </div>
                        </div>
                    </TransitionGroup>
                    <div
                        :class="[
                            'group flex w-full cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-take-navy-light transition-colors hover:border-take-yellow',
                        ]"
                        @click="showCreatePlannedCombatModal"
                    >
                        <FormButton
                            @clicked="showCreatePlannedCombatModal"
                            class="group-hover:text-take-yellow"
                            buttonColour="take-navy"
                            hoverButtonColour="take-navy"
                            textColour="take-grey"
                            hoverTextColour="take-yellow"
                            icon="plus"
                            label="Add Combat"
                            size="sm"
                        />
                    </div>
                </div>
            </aside>
            <section class="col-span-6 overflow-y-auto">
                <IndexPlannedCombatSection />
            </section>
        </section>
        <section class="flex flex-col items-center px-2" v-else key="addPlannedCombat">
            <h2 class="w-full text-center text-xl">Create your first planned combat</h2>
            <CreatePlannedCombatForm :onCreatePlannedCombat="onCreatePlannedCombat" />
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

const deleteCombatModal = ref<InstanceType<typeof ConfirmModal> | null>(null);
const createPlannedCombatModal = ref<InstanceType<typeof Modal> | null>(null);

const campaignStore = useCampaignStore();
const hasActiveCombat = computed(() => campaignStore.state.combatDto != null);
const plannedCombatStore = usePlannedCombatStore();
const plannedCombats = computed(() => campaignStore.state.plannedCombats);
const selectedPlannedCombat = computed(() => plannedCombatStore.selectedPlannedCombat);

async function setCombat(combat: PlannedCombat) {
    campaignStore.setPlannedCombat(combat.id);
}

// Create planned combat
async function showCreatePlannedCombatModal() {
    createPlannedCombatModal.value?.show();
}

async function onCreatePlannedCombat(
    input: void | Omit<CreatePlannedCombatRequest, "campaignId">
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
    combat: PlannedCombat
) {
    loadingCtrl.setLoaded();
    // If the combat has no stages, or all the stages are empty, then just delete without showing the modal.
    if (
        combat.stages?.length == 0 ||
        combat.stages?.flatMap((x) => x.NPCs).filter((x) => x != null).length == 0
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

async function deleteCombat(loadingCtrl: ButtonLoadingControl, combat: PlannedCombat) {
    loadingCtrl.setLoading();
    return await campaignStore.deletePlannedCombat(combat.id).then(() => {
        loadingCtrl.setLoaded();
        deleteCombatModal.value?.hide();
    });
}

async function onOpenCombat(plannedCombatId: string) {
    return await plannedCombatStore.createOpenCombat();
}

onMounted(() => {
    if (
        plannedCombats.value &&
        plannedCombats.value.length > 0 &&
        selectedPlannedCombat.value == null
    ) {
        campaignStore.setPlannedCombat(plannedCombats.value[0].id);
    }
});
</script>
