<template>
    <div>
        <div
            @click="() => editPlannedCharacterFormModal?.show()"
            class="p-2 flex justify-between">
            <span>{{ props.npc.name }}</span>
            <CampaignCombatCharacterStatsDisplay
                v-bind="{
                    initiative: npc.initiative.roll,
                    armourClass: npc.armourClass,
                    health: npc.health,
                }" />
        </div>
        <Dialog>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>
                        
                    </DialogTitle>
                </DialogHeader>
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
                            " />
                
            </DialogContent>
        </Dialog>
    </div>
</template>

<script setup lang="ts">
    import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
    import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
    import type { PlannedCombatCharacter } from "~/utils/types/models";
    import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

const modalOpen = ref(false);
    const props = defineProps<{
        npc: PlannedCombatCharacter;
        editNpc: (
            request: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">
        ) => Promise<any>;
        deleteNpc: (
            request: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">
        ) => Promise<any>;
    }>();
</script>
