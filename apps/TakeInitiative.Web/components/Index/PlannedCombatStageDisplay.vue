<template>
    <div class="flex w-full flex-col rounded-xl border-2 border-take-navy-light p-2">
        <div class="mb-2 flex w-full flex-row gap-2">
            <div class="flex-1 text-take-yellow">
                {{ props.stage.name }}
            </div>
            <FormButton
                icon="plus"
                buttonColour="take-navy-medium"
                textColour="white"
                @click="() => createNpcFormModal?.show()"
                size="sm"
            />
            <FormButton
                icon="trash"
                buttonColour="take-navy-medium"
                textColour="white"
                hoverButtonColour="take-red"
                @clicked="() => props.deleteStage()"
                size="sm"
            />
        </div>
        <section class="flex flex-col flex-1 gap-2 pb-4">
            <TransitionGroup class="flex flex-1 flex-col gap-2" tag="section" name="fade">
                <section
                    v-for="npc in stage.npcs"
                    :key="npc.id"
                    class="min-h-fit min-w-fit rounded-xl border border-take-navy-light hover:border-take-yellow cursor-pointer"
                >
                    <PlanedCombatNpcDisplay
                        class="p-2"
                        :npc="npc"
                        :editNpc="(request) => props.updateNpc(request)"
                        :deleteNpc="(request) => props.deleteNpc(request)"
                    />
                </section>
            </TransitionGroup>
            <Modal ref="createNpcFormModal" title="Create NPC">
                <ModifyPlannedCharacterForm
                    :onCreate="
                        (request) =>
                            props
                                .createNpc(request)
                                .then(() => createNpcFormModal?.hide())
                    "
                />
            </Modal>
        </section>
    </div>
</template>

<script setup lang="ts">
import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import type { PlannedCombatStage } from "~/utils/types/models";
import Modal from "~/components/Modal.vue";

import PlanedCombatNpcDisplay from "./PlanedCombatNpcDisplay.vue";

const createNpcFormModal = ref<InstanceType<typeof Modal> | null>(null);

const props = defineProps<{
    stage: PlannedCombatStage;
    deleteStage: () => Promise<any>;
    createNpc: (
        request: Omit<CreatePlannedCombatNpcRequest, "combatId" | "stageId">
    ) => Promise<any>;
    updateNpc: (
        request: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">
    ) => Promise<any>;
    deleteNpc: (
        request: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">
    ) => Promise<any>;
}>();
</script>
