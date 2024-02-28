<template>
    <Transition name="fade" mode="out-in">
        <main
            class="flex w-full flex-col gap-2 overflow-auto py-2 pl-4"
            v-if="plannedCombat"
            :key="plannedCombat.id"
        >
            <TransitionGroup
                class="flex flex-col gap-4 overflow-y-auto overflow-x-hidden"
                tag="body"
                name="fade"
            >
                <div
                    class="flex w-full justify-center overflow-y-auto"
                    v-for="stage in plannedCombat.stages"
                    :key="stage.id"
                >
                    <div class="w-full rounded-xl border-2 border-take-navy-light p-2">
                        <div class="mb-2 flex w-full flex-row gap-2">
                            <div class="flex-1 text-take-yellow">
                                {{ stage.name }}
                            </div>
                            <FormButton
                                icon="plus"
                                buttonColour="take-navy-medium"
                                textColour="white"
                                @click="() => showCreateNpcModal(stage)"
                                size="sm"
                            />
                            <FormButton
                                icon="trash"
                                buttonColour="take-navy-medium"
                                textColour="white"
                                hoverButtonColour="take-red"
                                @clicked="() => deleteStage(stage)"
                                size="sm"
                            />
                        </div>
                        <section class="overflow flex gap-2 overflow-x-auto pb-4">
                            <section
                                v-for="npc in stage.NPCs"
                                :key="npc.name"
                                class="min-w-fit cursor-pointer rounded-xl border border-take-navy-light p-2 hover:border-take-yellow"
                                @onclick="() => editNpc(stage, npc)"
                            >
                                <label class="cursor-pointer">
                                    {{ npc.name }} x {{ npc.quantity }}
                                </label>
                                <div class="flex justify-between gap-2">
                                    <div class="min-w-fit" v-if="npc.health">
                                        <FontAwesomeIcon icon="droplet" />
                                        {{ npc.health.currentHealth }}
                                        {{
                                            npc.health.maxHealth
                                                ? `/ ${npc.health.maxHealth}`
                                                : ""
                                        }}
                                    </div>
                                    <div class="min-w-fit" v-if="npc.armorClass">
                                        <FontAwesomeIcon icon="shield-halved" />
                                        {{ npc.armorClass }}
                                    </div>
                                    <div class="min-w-fit">
                                        <FontAwesomeIcon icon="shoe-prints" />
                                        {{ npc.initiative }}
                                    </div>
                                </div>
                            </section>
                            <section
                                class="min-w-fit cursor-pointer rounded-xl border-2 border-dashed border-take-navy-light p-2 hover:border-take-yellow group transition-colors"
                                @click="() => showCreateNpcModal(stage)"
                            >
                                <FormButton
                                    class="group-hover:text-take-yellow"
                                    icon="plus"
                                    label="Add NPC"
                                    buttonColour="take-navy"
                                    hoverButtonColour="take-navy"
                                    textColour="white"
                                    hoverTextColour="take-yellow"
                                />
                            </section>
                        </section>
                    </div>
                </div>
            </TransitionGroup>
            <footer
                class="flex w-full cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-take-navy-light p-2 hover:border-take-yellow group transition-colors"
                @click="showCreateStageModal"
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
                <CreatePlannedCombatStageForm
                    :stage="lastedClickedStage!"
                    :onSubmit="createStage"
                />
            </Modal>
            <Modal ref="createNpcFormModal" title="Create NPC">
                <CreateNpcForm :onSubmit="addNpc" />
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

const createNpcFormModal = ref<InstanceType<typeof Modal> | null>(null);
const createStageModal = ref<InstanceType<typeof Modal> | null>(null);
const campaignStore = useCampaignStore();
const plannedCombatStore = usePlannedCombatStore();
const plannedCombat = computed(() => plannedCombatStore.selectedPlannedCombat);

const emit = defineEmits<{
    (e: "DeleteCombat", loadingCtrl: ButtonLoadingControl): void;
    (e: "UpdateCombat", plannedCombat: PlannedCombat): void;
}>();

// Create NPC
const lastedClickedStage = ref<PlannedCombatStage | null>(null);
function showCreateNpcModal(stage: PlannedCombatStage) {
    lastedClickedStage.value = stage;
    createNpcFormModal.value?.show();
}

function showCreateStageModal() {
    createStageModal.value?.show();
}

async function createStage(
    input: void | Omit<CreatePlannedCombatStageRequest, "combatId">
) {
    return await plannedCombatStore
        .addStage(input!)
        .then(() => createStageModal.value?.hide());
}

async function addNpc(
    nonPlayerCharacter: Omit<CreatePlannedCombatNpcRequest, "combatId" | "stageId">
) {
    return await plannedCombatStore
        .addNpc(lastedClickedStage.value!, nonPlayerCharacter)
        .then(() => createNpcFormModal.value?.hide())
        .catch(() => createNpcFormModal.value);
}

function editNpc(
    stage: PlannedCombatStage,
    npc: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">
) {}

async function deleteStage(stage: PlannedCombatStage) {
    return await plannedCombatStore.removeStage(stage.id);
}
</script>

<style></style>
