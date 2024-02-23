<template>
    <main class="w-full overflow-auto py-2 pl-4" v-if="plannedCombat">
        <Teleport to="#IndexPageTabs">
            <header class="mb-2 flex select-none flex-row text-take-yellow">
                <div class="flex flex-1 justify-center">
                    <label class="flex items-center">
                        {{ plannedCombat.combatName }}</label
                    >
                </div>
                <div>
                    <FormButton
                        icon="trash"
                        textColour="white"
                        buttonColour="take-navy-light"
                        hoverButtonColour="take-red"
                        @clicked="(ctrl) => emit('DeleteCombat', ctrl)"
                    />
                </div>
            </header>
        </Teleport>

        <body class="flex flex-col gap-4 overflow-y-auto">
            <div
                class="flex w-full justify-center overflow-y-auto"
                v-for="stage in plannedCombat.stages"
                :key="stage.id"
            >
                <div
                    class="w-full rounded-xl border-2 border-take-navy-light p-2"
                >
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
                    <div class="overflow flex gap-2 overflow-x-auto pb-4">
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
                            class="min-w-fit cursor-pointer rounded-xl border-2 border-dashed border-take-navy-light p-2 hover:border-take-yellow"
                            @click="() => showCreateNpcModal(stage)"
                        >
                            <div class="p-2">
                                <FontAwesomeIcon icon="plus" />
                            </div>
                        </section>
                    </div>
                </div>
            </div>
        </body>

        <footer class="flex justify-center">
            <div
                class="min-w-fit cursor-pointer rounded-xl border-2 border-dashed border-take-navy-light p-2 hover:border-take-yellow"
                @click="showCreateStageModal"
            >
                <div class="p-2">
                    <FontAwesomeIcon icon="plus" />
                </div>
            </div>
        </footer>

        <Modal ref="createStageModal" title="Create Stage">
            <CreatePlannedCombatStageForm
                :stage="lastedClickedStage!"
                :onSubmit="createStage"
            />
        </Modal>

        <Modal ref="createNpcFormModal" title="Create NPC">
            <CreateNpcForm
                :stage="lastedClickedStage!"
                :onSubmit="(stage, npc) => addNpc(stage, npc)"
            />
        </Modal>
    </main>
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
    input: void | Omit<CreatePlannedCombatStageRequest, "combatId">,
) {
    return await plannedCombatStore
        .addStage(input!)
        .then(() => createStageModal.value?.hide());
}

async function addNpc(
    stage: PlannedCombat,
    nonPlayerCharacter: PlannedCombatNonPlayerCharacter,
) {}

function editNpc(
    stage: PlannedCombatStage,
    npc: PlannedCombatNonPlayerCharacter,
) {}

function deleteStage(stage: PlannedCombatStage) {}
</script>
