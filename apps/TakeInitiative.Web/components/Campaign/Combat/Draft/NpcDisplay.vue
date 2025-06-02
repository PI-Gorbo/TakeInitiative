<template>
    <Sheet v-model:open="open">
        <SheetTrigger asChild>
            <Button
                variant="outline"
                @click="() => (open = true)"
                class="p-2 flex justify-between interactable w-full">
                <span>{{ props.npc.name }} (*{{ props.npc.quantity }})</span>
                <CampaignCombatCharacterStatsDisplay
                    v-bind="{
                        initiative: npc.initiative.roll,
                        armourClass: npc.armourClass,
                        health: npc.health,
                    }" />
            </Button>
        </SheetTrigger>
        <SheetContent>
            <SheetHeader>
                <SheetTitle>Edit NPC</SheetTitle>
            </SheetHeader>
            <CampaignCombatDraftModifyNpcForm
                v-bind="{
                    npc: props.npc,
                    onEdit: async (request) => {
                        await props.editNpc(request);
                        open = false;
                    },
                    onDelete: async (request) => {
                        await props.deleteNpc(request);
                        open = false;
                    },
                }" />
        </SheetContent>
    </Sheet>
</template>

<script setup lang="ts">
    import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
    import type { DraftCombatCharacter } from "~/utils/types/models";
    import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";

    const open = ref(false);
    const props = defineProps<{
        npc: DraftCombatCharacter;
        editNpc: (
            request: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">
        ) => Promise<any>;
        deleteNpc: (
            request: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">
        ) => Promise<any>;
    }>();
</script>
