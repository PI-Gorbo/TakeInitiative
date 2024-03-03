<template>
    <div @click="() => editNpcFormModal?.show()" class="p-2">
        <div class="cursor-pointer flex gap-2 justify-between px-2">
            <div class="cursor-pointer text-lg ws-nowrap select-none">
                {{ npc.name }} ( x {{ npc.quantity }} )
            </div>
            <div v-if="npc.health">
                <FontAwesomeIcon icon="droplet" />
                {{ npc.health.currentHealth }}
                {{ npc.health.maxHealth ? `/ ${npc.health.maxHealth}` : "" }}
            </div>
            <div v-if="npc.armorClass">
                <FontAwesomeIcon icon="shield-halved" />
                <div class="ws-nowrap min-w-fit">{{ npc.armorClass }}</div>
            </div>
            <div class="flex gap-2 items-center select-none">
                <FontAwesomeIcon icon="shoe-prints" />
                <div>{{ npc.initiative.value }}</div>
            </div>
        </div>
    </div>
    <Modal ref="editNpcFormModal" title="Edit NPC">
        <ModifyNpcForm
            :npc="props.npc"
            :onEdit="
                (request) => props.editNpc(request).then(() => editNpcFormModal?.hide())
            "
            :onDelete="
                (request) => props.deleteNpc(request).then(() => editNpcFormModal?.hide())
            "
        />
    </Modal>
</template>

<script setup lang="ts">
import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import type { PlannedCombatNonPlayerCharacter } from "~/utils/types/models";
import Modal from "~/components/Modal.vue";
import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
const editNpcFormModal = ref<InstanceType<typeof Modal> | null>(null);
const props = defineProps<{
    npc: PlannedCombatNonPlayerCharacter;
    editNpc: (
        request: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">
    ) => Promise<any>;
    deleteNpc: (
        request: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">
    ) => Promise<any>;
}>();
</script>
