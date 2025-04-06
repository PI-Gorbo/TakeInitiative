<template>
    <Collapsible :defaultOpen="true" class="w-full">
        <CollapsibleTrigger
            class="flex items-center justify-between gap-2 w-full">
            <CampaignCombatDraftStageNameForm
                :initalStageName="props.stage.name"
                :allStageNames="props.allStages.map((x) => x.name)"
                :updateStageName="(name) => props.updateStage({ name })" />
            <!-- <div class="flex flex-row gap-2">
                <FormButton
                    icon="plus"
                    label="Add NPC"
                    buttonColour="take-purple-light"
                    textColour="white"
                    @clicked="showCreatePlannedCharacterModal" />
                </div> -->
            <Button
                size="icon"
                variant="destructive"
                @clicked="() => props.deleteStage()">
                <FontAwesomeIcon :icon="faTrash" />
            </Button>
        </CollapsibleTrigger>
        <CollapsibleContent>
            <section class="flex flex-1 flex-col gap-2 pb-4">
                <section v-for="npc in npcList" :key="npc.id">
                    <CampaignCombatDraftNpcDisplay
                        class="p-2"
                        :npc="npc"
                        :editNpc="(request) => props.updateNpc(request)"
                        :deleteNpc="(request) => props.deleteNpc(request)" />
                </section>
                <!-- <TransitionGroup
                    class="flex flex-1 flex-col gap-2"
                    tag="section"
                    name="fade">
                   
                </TransitionGroup>
                <Modal
                    ref="createPlannedCharacterFormModal"
                    title="Create Character">
                    <CampaignCombatDraftModifyNpcForm
                        :onCreate="(request) => props.createNpc(request)" />
                </Modal> -->
            </section>
        </CollapsibleContent>
    </Collapsible>
</template>

<script setup lang="ts">
    import { faTrash } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
    import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
    import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
    import type { UpdatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/updatePlannedCombatStageRequest";
    import type { PlannedCombatStage } from "~/utils/types/models";

    const props = defineProps<{
        allStages: PlannedCombatStage[];
        stage: PlannedCombatStage;
        updateStage: (
            req: Omit<UpdatePlannedCombatStageRequest, "combatId" | "stageId">
        ) => Promise<any>;
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

    const stageName = ref<string>(props.stage.name);
    const npcList = computed(() =>
        props.stage.npcs.sort((npc1, npc2) => {
            // Sort Alphabetically, then by Id.
            if (npc1.name < npc2.name) return -1;
            if (npc1.name > npc2.name) return 1;
            if (npc1.id < npc2.id) return -1;
            if (npc1.id > npc2.id) return 1;
            return 0;
        })
    );

    const showCreatePlannedCharacterModal = () => {
        console.log("show planned character modal");
    };
</script>
