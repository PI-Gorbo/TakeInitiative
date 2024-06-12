<template>
    <div @click="() => editPlannedCharacterFormModal?.show()" class="p-2">
        <div class="flex cursor-pointer justify-between gap-2 px-2">
            <div class="ws-nowrap cursor-pointer select-none text-lg">
                {{ npc.name }} ( x {{ npc.quantity }} )
            </div>
            <CharacterHealthDisplay :health="npc.health" />
            <div
                v-if="npc.armourClass"
                class="flex select-none items-center gap-2"
            >
                <FontAwesomeIcon icon="shield-halved" />
                <div class="ws-nowrap min-w-fit">{{ npc.armourClass }}</div>
            </div>
            <div class="flex select-none items-center gap-2">
                <FontAwesomeIcon icon="shoe-prints" />
                <div>{{ npc.initiative.value }}</div>
            </div>
        </div>
    </div>
    <Modal ref="editPlannedCharacterFormModal" title="Edit Planned Character">
        <IndexModifyPlannedCharacterForm
            :npc="props.npc"
            :onEdit="
                (request) =>
                    props
                        .editNpc(request)
                        .then(() => editPlannedCharacterFormModal?.hide())
            "
            :onDelete="
                (request) =>
                    props
                        .deleteNpc(request)
                        .then(() => editPlannedCharacterFormModal?.hide())
            "
        />
    </Modal>
</template>

<script setup lang="ts">
import type { CreatePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import type { UpdatePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import type { PlannedCombatCharacter } from "base/utils/types/models";
import Modal from "base/components/Modal.vue";
import type { DeletePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
const editPlannedCharacterFormModal = ref<InstanceType<typeof Modal> | null>(
    null,
);
const props = defineProps<{
    npc: PlannedCombatCharacter;
    editNpc: (
        request: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">,
    ) => Promise<any>;
    deleteNpc: (
        request: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">,
    ) => Promise<any>;
}>();
</script>
