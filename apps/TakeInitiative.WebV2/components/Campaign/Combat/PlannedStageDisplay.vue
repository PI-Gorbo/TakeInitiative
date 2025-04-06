<template>
    <div
        class="flex w-full flex-col rounded-xl border-2 border-take-purple p-2">
        <div
            class="mb-2 flex w-full flex-row flex-wrap items-center justify-between gap-2">
            <FormToggleableInput
                :value="stageName"
                textColour="take-yellow"
                colour="take-navy-medium"
                notEditableColour="take-navy"
                :autoFocus="true"
                @update:value="(val) => (stageName = String(val))"
                :onSave="() => props.updateStage({ name: stageName })" />
            <div class="flex flex-row gap-2">
                <FormButton
                    icon="plus"
                    label="Add NPC"
                    buttonColour="take-purple-light"
                    textColour="white"
                    @clicked="showCreatePlannedCharacterModal" />
                <FormButton
                    icon="trash"
                    buttonColour="take-purple-light"
                    textColour="white"
                    hoverButtonColour="take-red"
                    @clicked="() => props.deleteStage()" />
            </div>
        </div>
        <section class="flex flex-1 flex-col gap-2 pb-4">
            <TransitionGroup
                class="flex flex-1 flex-col gap-2"
                tag="section"
                name="fade">
                <section
                    v-for="npc in npcList"
                    :key="npc.id"
                    class="min-h-fit min-w-fit cursor-pointer rounded-xl border border-take-purple-light hover:border-take-yellow">
                    <PlanedCombatNpcDisplay
                        class="p-2"
                        :npc="npc"
                        :editNpc="(request) => props.updateNpc(request)"
                        :deleteNpc="(request) => props.deleteNpc(request)" />
                </section>
            </TransitionGroup>
            <Modal
                ref="createPlannedCharacterFormModal"
                title="Create Character">
                <IndexModifyPlannedCharacterForm
                    :onCreate="
                        (request) =>
                            props
                                .createNpc(request)
                                .then(() =>
                                    createPlannedCharacterFormModal?.hide()
                                )
                    " />
            </Modal>
        </section>
    </div>
</template>

<script setup lang="ts">
    import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
    import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
    import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
    import type { UpdatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/updatePlannedCombatStageRequest";
    import type { PlannedCombatStage } from "~/utils/types/models";

    import PlanedCombatNpcDisplay from "./PlanedCombatNpcDisplay.vue";

    const createPlannedCharacterFormModal = ref<InstanceType<
        typeof Modal
    > | null>(null);

    const props = defineProps<{
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
        createPlannedCharacterFormModal.value?.show();
    };
</script>
