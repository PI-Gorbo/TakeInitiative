<template>
    <Transition name="fade" mode="out-in">
        <main
            class="flex h-full w-full flex-col gap-4 overflow-auto px-2"
            v-if="plannedCombat"
            :key="plannedCombat.id"
        >
            <TransitionGroup
                v-if="plannedCombat?.stages?.length != 0"
                class="flex flex-1 flex-col"
                tag="body"
                name="fade"
            >
                <div
                    class="flex w-full flex-1 justify-center pb-1"
                    v-for="stage in plannedCombat.stages"
                    :key="stage.id"
                >
                    <PlannedCombatStageDisplay
                        :stage="stage"
                        :updateStage="(req) => updateStage(stage, req)"
                        :deleteStage="() => deleteStage(stage)"
                        :createNpc="(request) => addNpc(stage, request)"
                        :updateNpc="(request) => updateNpc(stage, request)"
                        :deleteNpc="(request) => deleteNpc(stage, request)"
                    />
                </div>
            </TransitionGroup>
            <footer
                class="group flex w-full cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-take-navy-light p-2 transition-colors hover:border-take-yellow"
                @click="createStageModal?.show()"
            >
                <FormButton
                    class="group-hover:text-take-yellow"
                    icon="plus"
                    label="Add Stage"
                    buttonColour="take-navy"
                    hoverButtonColour="take-navy"
                    textColour="white"
                    hoverTextColour="take-yellow"
                    @clicked="createStageModal?.show()"
                />
            </footer>
            <Modal ref="createStageModal" title="Create Stage">
                <CreatePlannedCombatStageForm :onSubmit="createStage" />
            </Modal>
        </main>
    </Transition>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type {
    PlannedCombat,
    PlannedCombatCharacter,
    PlannedCombatStage,
} from "base/utils/types/models";
import type ConfirmModalVue from "../ConfirmModal.vue";
import type { ButtonLoadingControl } from "../Form/Button.vue";
import Modal from "base/components/Modal.vue";
import type { CreatePlannedCombatRequest } from "base/utils/api/plannedCombat/createPlannedCombatRequest";
import type { CreatePlannedCombatStageRequest } from "base/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
import type { CreatePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import type { UpdatePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import PlannedCombatStageDisplay from "./PlannedCombatStageDisplay.vue";
import type { DeletePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
import type { UpdatePlannedCombatStageRequest } from "base/utils/api/plannedCombat/stages/updatePlannedCombatStageRequest";

const campaignStore = useCampaignStore();
const campaignCombatStore = useCampaignCombatsStore();
const { selectedPlannedCombat: plannedCombat } =
    storeToRefs(campaignCombatStore);

const emit = defineEmits<{
    (e: "DeleteCombat", loadingCtrl: ButtonLoadingControl): void;
    (e: "UpdateCombat", plannedCombat: PlannedCombat): void;
}>();

const createStageModal = ref<InstanceType<typeof Modal> | null>(null);
async function createStage(
    input: void | Omit<CreatePlannedCombatStageRequest, "combatId">,
) {
    return await campaignCombatStore
        .addStage(input!)
        .then(() => createStageModal.value?.hide());
}

async function addNpc(
    stage: PlannedCombatStage,
    nonPlayerCharacter: Omit<
        CreatePlannedCombatNpcRequest,
        "combatId" | "stageId"
    >,
) {
    return await campaignCombatStore.addNpc(stage, nonPlayerCharacter);
}

async function updateNpc(
    stage: PlannedCombatStage,
    npc: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">,
) {
    return await campaignCombatStore.updateNpc(stage, npc);
}

async function deleteNpc(
    stage: PlannedCombatStage,
    npc: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">,
) {
    return await campaignCombatStore.removeNpc(stage, npc.npcId);
}

async function deleteStage(stage: PlannedCombatStage) {
    return await campaignCombatStore.removeStage(stage.id);
}

async function updateStage(
    stage: PlannedCombatStage,
    req: Omit<UpdatePlannedCombatStageRequest, "combatId" | "stageId">,
) {
    debugger
    return await campaignCombatStore.updateStage({ stageId: stage.id, ...req });
}
</script>
