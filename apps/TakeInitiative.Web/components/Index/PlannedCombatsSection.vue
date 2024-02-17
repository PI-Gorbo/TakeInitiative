<template>
    <div class="h-full w-full">
        <div
            v-if="!state.selectedPlannedCombat"
            class="flex h-full w-full justify-center"
        >
            <div class="flex h-full w-full justify-center py-2">
                <div class="flex h-full w-full flex-col gap-4">
                    <div @onclick="showCreatePlannedCombatModal" class="flex justify-end">
                        <IconButton
                            IconName="fa-plus"
                            Colour="TakeInitiativeColour.TakeNavyMedium"
                        />
                    </div>

                    <div
                        v-for="combat in plannedCombats"
                        :key="combat.id"
                        class="group flex cursor-pointer select-none gap-4 rounded-xl border-2 border-take-navy-light border-opacity-100 transition-colors hover:border-take-yellow"
                        @click="() => setCombat(combat)"
                    >
                        <h3
                            class="rounded-l-lg-fix flex w-1/6 items-center justify-center bg-take-navy-light text-lg transition-colors group-hover:bg-take-yellow group-hover:text-take-navy"
                        >
                            {{ combat.combatName }}
                        </h3>
                        <div class="flex items-center justify-center py-2">
                            {{ combat.stages?.length }}
                        </div>
                        <div class="flex items-center justify-center py-2">NPC COUNT</div>
                        <div class="flex flex-1 items-center justify-end py-2">
                            <FormButton
                                icon="trash"
                                buttonColour="take-navy-light"
                                hoverButtonColour="take-red"
                                hoverTextColour="take-navy"
                                @clicked="() => showDeleteCombatModal(combat)"
                            />
                        </div>
                    </div>

                    <div
                        class="group flex w-full cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-take-navy-light py-2 transition-colors hover:border-take-yellow"
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
                            iconSize="lg"
                        />
                    </div>
                </div>
            </div>
        </div>
        <div v-else>
            <IndexPlannedCombatSection />
        </div>

        <Modal ref="createPlannedCombatModal" title="Create a planned combat">
            <div>This is a test modal</div>
        </Modal>
    </div>
</template>
<script setup lang="ts">
import type { PlannedCombat } from "~/utils/types/models.ts";
import Modal from "~/components/Modal.vue";
const campaignStore = useCampaignStore();
const plannedCombats = computed(() => campaignStore.state.plannedCombats);
const state = reactive({
    selectedPlannedCombat: null as PlannedCombat | null,
});
const createPlannedCombatModal = ref<InstanceType<typeof Modal> | null>(null);
async function showCreatePlannedCombatModal() {
    createPlannedCombatModal.value?.show();
}

async function setCombat(combat: PlannedCombat) {}

async function showDeleteCombatModal(combat: PlannedCombat) {}
</script>
