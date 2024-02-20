<template>
    <div class="h-full w-full">
        <section
            class="grid h-full w-full grid-cols-8"
            v-if="plannedCombats && plannedCombats.length > 0"
        >
            <aside
                class="col-span-2 flex flex-col gap-2 overflow-y-auto rounded-xl border border-take-navy-medium px-2 py-1"
            >
                <div
                    v-for="combat in plannedCombats"
                    :key="combat.id"
                    :class="[
                        'group flex cursor-pointer select-none gap-4 rounded-xl border border-take-navy-medium border-opacity-100 bg-take-navy-medium p-1 transition-colors hover:border-take-yellow',
                        {
                            'border-take-yellow':
                                combat.id ==
                                campaignStore.state.selectedPlannedCombat?.id,
                        },
                    ]"
                    @click="() => setCombat(combat)"
                >
                    {{ combat.combatName }}
                </div>
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
                        size="sm"
                    />
                </div>
            </aside>
            <section class="col-span-6">
                <IndexPlannedCombatSection
                    :plannedCombat="campaignStore.state.selectedPlannedCombat!"
                    @deleteCombat="
                        (ctrl: ButtonLoadingControl) =>
                            showDeleteCombatModal(
                                ctrl,
                                campaignStore.state.selectedPlannedCombat!,
                            )
                    "
                />
            </section>
        </section>
        <section class="flex flex-col items-center px-2" v-else>
            <h2 class="w-full text-center text-xl">
                Create your first planned combat
            </h2>
            <CreatePlannedCombatForm
                :onCreatePlannedCombat="onCreatePlannedCombat"
                v-model:campaignName="
                    createPlannedCombatForm.combatName.value.value
                "
                :inputProps="createPlannedCombatForm.combatName.props.value"
            />
        </section>

        <Modal ref="createPlannedCombatModal" title="Create a planned combat">
            <CreatePlannedCombatForm
                class="h-full w-full"
                :onCreatePlannedCombat="onCreatePlannedCombat"
                v-model:campaignName="
                    createPlannedCombatForm.combatName.value.value
                "
                :inputProps="createPlannedCombatForm.combatName.props.value"
            />
        </Modal>

        <ConfirmModal
            ref="deleteCombatModal"
            confirmText="Delete"
            confirmColour="take-red"
            :closeOnConfirm="false"
            @Confirm="
                (ctrls) =>
                    deleteCombat(
                        ctrls,
                        campaignStore.state.selectedPlannedCombat,
                    )
            "
            cancelText="Cancel"
            cancelColour="take-yellow"
            bodyText="Delete this planned combat?"
        />
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { PlannedCombat } from "~/utils/types/models.ts";
import Modal from "~/components/Modal.vue";
import ConfirmModal from "~/components/ConfirmModal.vue";
import type { ButtonLoadingControl } from "../Form/Button.vue";

const deleteCombatModal = ref<InstanceType<typeof ConfirmModal> | null>(null);
const createPlannedCombatModal = ref<InstanceType<typeof Modal> | null>(null);

const campaignStore = useCampaignStore();
const plannedCombats = computed(() => campaignStore.state.plannedCombats);
const state = reactive({
    selectedPlannedCombat: null as PlannedCombat | null,
});

async function setCombat(combat: PlannedCombat) {
    campaignStore.setPlannedCombat(combat.id);
}

// Create planned combat
const createPlannedCombatForm = useCreatePlannedCombatForm();
async function showCreatePlannedCombatModal() {
    createPlannedCombatModal.value?.show();
}

async function onCreatePlannedCombat() {
    return await createPlannedCombatForm
        .submit()
        .then((plannedCombat) => {
            return campaignStore.setPlannedCombat(plannedCombat.id);
        })
        .then(() => createPlannedCombatModal.value?.hide())
        .catch();
}

// Delete combat
async function showDeleteCombatModal(
    loadingCtrl: ButtonLoadingControl,
    combat: PlannedCombat,
) {
    // If the combat has no stages, or all the stages are empty, then just delete without showing the modal.
    if (
        combat.stages?.length == 0 ||
        combat.stages?.flatMap((x) => x.NPCs).filter((x) => x != null).length ==
            0
    ) {
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
    console.log("Before loading control is used");
    loadingCtrl.setLoading();
    return await campaignStore.deletePlannedCombat(combat.id).then(() => {
        console.log("Finished deleting combat");
        loadingCtrl.setLoaded();
        deleteCombatModal.value?.hide();
    });
}

onMounted(() => {
    if (
        plannedCombats.value &&
        plannedCombats.value.length > 0 &&
        campaignStore.state.selectedPlannedCombat == null
    ) {
        campaignStore.setPlannedCombat(plannedCombats.value[0].id);
    }
});
</script>
