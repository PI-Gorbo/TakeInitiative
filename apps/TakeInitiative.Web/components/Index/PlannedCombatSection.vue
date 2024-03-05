<template>
    <Transition name="fade" mode="out-in">
        <main
            class="flex h-full w-full flex-col gap-4 overflow-auto py-2 pl-4"
            v-if="plannedCombat"
            :key="plannedCombat.id"
        >
            <header>
                <FormButton
                    label="Open Combat"
                    @clicked="
                        () => {
                            campaignStore.openCombat(plannedCombat?.id!);
                        }
                    "
                    size="sm"
                />
            </header>
            <TransitionGroup
                v-if="plannedCombat?.stages?.length != 0"
                class="flex flex-col gap-4"
                tag="body"
                name="fade"
            >
                <div
                    class="flex w-full flex-1 justify-center"
                    v-for="stage in plannedCombat.stages"
                    :key="stage.id"
                >
                    <PlannedCombatStageDisplay
                        :stage="stage"
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
    PlannedCombatNonPlayerCharacter,
    PlannedCombatStage,
} from "~/utils/types/models";
import type ConfirmModalVue from "../ConfirmModal.vue";
import type { ButtonLoadingControl } from "../Form/Button.vue";
import Modal from "~/components/Modal.vue";
import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";
import type { CreatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import PlannedCombatStageDisplay from "./PlannedCombatStageDisplay.vue";
import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";

const campaignStore = useCampaignStore();
const plannedCombatStore = usePlannedCombatStore();
const plannedCombat = computed(() => plannedCombatStore.selectedPlannedCombat);

const emit = defineEmits<{
    (e: "DeleteCombat", loadingCtrl: ButtonLoadingControl): void;
    (e: "UpdateCombat", plannedCombat: PlannedCombat): void;
}>();

const createStageModal = ref<InstanceType<typeof Modal> | null>(null);
async function createStage(
    input: void | Omit<CreatePlannedCombatStageRequest, "combatId">,
) {
    return await plannedCombatStore
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
    return await plannedCombatStore.addNpc(stage, nonPlayerCharacter);
}

async function updateNpc(
    stage: PlannedCombatStage,
    npc: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">,
) {
    return await plannedCombatStore.updateNpc(stage, npc);
}

async function deleteNpc(
    stage: PlannedCombatStage,
    npc: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">,
) {
    return await plannedCombatStore.removeNpc(stage, npc.npcId);
}

async function deleteStage(stage: PlannedCombatStage) {
    return await plannedCombatStore.removeStage(stage.id);
}
</script>

<style></style>
