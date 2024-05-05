<template>
    <TransitionGroup name="fade" class="h-full w-full">
        <section
            key="plannedCombatList"
            class="grid h-full w-full grid-cols-9"
            v-if="plannedCombats.length != 0 || combatHistory.length != 0"
        >
           
           
        </section>
        
        <section
            class="flex flex-col items-center px-2"
            v-else
            key="addPlannedCombat"
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

// Modals
const deleteCombatModal = ref<InstanceType<typeof ConfirmModal> | null>(null);
const createPlannedCombatModal = ref<InstanceType<typeof Modal> | null>(null);

// Page State
const pageState = reactive<{
    page: {
        name: 'List'
    } | {
        name: 'PlannedCombat'
    } | {
        name: 'Completed Combat'
    }
}>({
    page: {
        name: 'List'
    }
})

const campaignStore = useCampaignStore();
const hasActiveCombat = computed(() => campaignStore.state.combatDto != null);
const plannedCombatStore = usePlannedCombatStore();
const plannedCombats = computed(() => campaignStore.state.plannedCombats ?? []);
const combatHistory = computed(() => campaignStore.state.finishedCombats ?? [])
const selectedPlannedCombat = computed(
    () => plannedCombatStore.selectedPlannedCombat,
);

async function setCombat(combat: PlannedCombat) {
    campaignStore.setPlannedCombat(combat.id);
}

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
