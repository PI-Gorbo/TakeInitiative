<template>
    <div class="flex h-full w-full justify-center">
        <div
            class="flex h-full w-full flex-1 flex-col overflow-y-auto md:w-4/5 md:max-w-[1200px]"
        >
            <!-- MAIN CONTENT -->
            <header class="grid grid-cols-3 p-2">
                <div class="">
                    <FormButton
                        v-if="!combatIsFinished"
                        class="w-max"
                        label="Staged List"
                        :icon="combatHasStarted ? 'plus' : 'users'"
                        @clicked="
                            () => {
                                showModal(
                                    combatHasStarted
                                        ? 'Combat Opened Stage Characters'
                                        : 'Combat Started Staged Character Menu',
                                );
                                toggleDrawer();
                            }
                        "
                        buttonColour="take-purple-light"
                        hoverButtonColour="take-yellow"
                    />
                </div>
                <div>
                    <label
                        class="flex items-center justify-center text-center font-NovaCut text-lg"
                        >{{ combat?.combatName }}</label
                    >
                    <label
                        class="flex items-center justify-center text-center font-NovaCut text-sm"
                        v-if="combatInInitiative"
                        >Round {{ combat?.roundNumber }}</label
                    >
                </div>
                <div class="flex justify-end gap-1 px-1">
                    <FormButton
                        v-if="userIsDm && combatHasStarted"
                        label="Start"
                        icon="shoe-prints"
                        :click="
                            () => combatStore.startCombat().then(toggleDrawer)
                        "
                        buttonColour="take-purple-light"
                        hoverButtonColour="take-navy-medium"
                        hoverTextColour="white"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'Starting...',
                        }"
                    />
                    <FormButton
                        v-else-if="userIsDm && combatInInitiative"
                        label="Finish"
                        icon="flag-checkered"
                        :click="
                            () => combatStore.finishCombat().then(toggleDrawer)
                        "
                        buttonColour="take-purple-light"
                        hoverButtonColour="take-navy-medium"
                        hoverTextColour="white"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'Finishing...',
                        }"
                    />
                </div>
            </header>
            <main
                :class="[
                    'flex w-full flex-1 flex-row justify-center overflow-y-auto px-2 pb-2',
                ]"
            >
                <section class="flex flex-1 flex-col gap-2 overflow-y-auto">
                    <CombatCharacterListSection
                        class="p-2"
                        @combatOpenedStageCharacters="
                            () => showModal('Combat Opened Stage Characters')
                        "
                        @OnClickCharacter="
                            (character) => onClickCharacter(character!)
                        "
                    />
                </section>
            </main>
            <footer class="flex items-center justify-between px-2 pb-2">
                <div class="rounded-md bg-take-purple p-2 text-sm">
                    Phase:
                    {{
                        combatHasStarted
                            ? "Started"
                            : combatInInitiative
                              ? "Initiative Rolled"
                              : "Finished"
                    }}
                </div>
                <FormButton
                    v-if="
                        combatInInitiative &&
                        (combat?.initiativeList.length ?? 0) > 0
                    "
                    icon="forward-step"
                    label="End Turn"
                    buttonColour="take-red"
                    hoverButtonColour="take-yellow-dark"
                    :disabled="!isUsersTurn"
                    :loadingDisplay="{
                        showSpinner: true,
                        loadingText: 'End...',
                    }"
                    :click="combatStore.endTurn"
                />
                <FormButton
                    v-else-if="combatIsFinished"
                    icon="house"
                    label="Go Home"
                    buttonColour="take-purple"
                    hoverButtonColour="take-yellow-dark"
                    :loadingDisplay="{
                        showSpinner: true,
                        loadingText: 'End...',
                    }"
                    :click="goHome"
                />
            </footer>
        </div>
        <Modal ref="combatPageModal" :title="modalState.title ?? ''">
            <CombatStagedCharactersSection
                v-if="
                    modalState.modalType ==
                    'Combat Started Staged Character Menu'
                "
                @RolledCharactersIntoInitiative="() => closeModal()"
            />
            <CombatModifyStagedCharacterForm
                v-if="modalState.modalType == 'Edit Staged Character'"
                :character="lastClickedStagedCharacter!"
                :onEdit="(req) => onUpsertStagedCharacter(req).then(closeModal)"
                :onDelete="onDeleteStagedCharacter"
            />
            <CombatAddStagedCharacterTabs
                v-if="modalState.modalType == 'Combat Opened Stage Characters'"
                :characterAdded="closeModal"
            />
            <div v-if="modalState.modalType == 'Edit Initiative Character'">
                <CombatEditInitiativeCharacterForm
                    v-if="lastClickedInitiativeCharacter"
                    :character="lastClickedInitiativeCharacter"
                    :onEdit="
                        (character) =>
                            combatStore
                                .updateInitiativeCharacter(character)
                                .then(closeModal)
                    "
                    :onDelete="
                        (character) =>
                            combatStore
                                .deleteInitiativeCharacter(character)
                                .then(closeModal)
                    "
                />
            </div>
        </Modal>
    </div>
</template>
<script setup lang="ts">
import Modal from "base/components/Modal.vue";
import type { DeleteStagedCharacterRequest } from "base/utils/api/combat/deleteStagedCharacterRequest";
import type { PostStagePlannedCharactersRequest } from "base/utils/api/combat/postStagePlannedCharactersRequest";
import type { StagedCharacterDTO } from "base/utils/api/combat/putUpsertStagedCharacter";
import {
    type InitiativeCharacter,
    type StagedCharacter,
} from "base/utils/types/models";

const campaignStore = useCampaignStore();
const userStore = useUserStore();
const combatDrawer = ref<HTMLInputElement | null>(null);
function toggleDrawer() {
    if (combatDrawer.value) {
        combatDrawer.value.checked = !combatDrawer.value?.checked;
    }
}

definePageMeta({
    requiresAuth: true,

    middleware: [
        function (to, from) {
            if (!process.client || to.name !== "combat-id") {
                return;
            }

            const combatStore = useCombatStore();
            combatStore.joinCombat();
        },
        function (to) {
            if (!process.client || to.name !== "combat-id") {
                return;
            }

            const combatStore = useCombatStore();
            combatStore.joinCombat();
        },
    ],
});

// Route
const route = useRoute();
const combatId = route.params.id;

// Combat data
const combatStore = useCombatStore();
const {
    combat,
    userIsDm,
    combatIsOpen: combatHasStarted,
    combatIsStarted: combatInInitiative,
    combatIsFinished,
    anyPlannedCharacters,
} = storeToRefs(combatStore);

// Main fetch
const { refresh, pending, error } = await useAsyncData("Combat", async () => {
    return await combatStore.setCombat(combatId as string).then(() => true);
});

// Ensures the user is joined whenever the page loads.
const { refresh: rejoinCombat } = await useAsyncData(
    "JoinCombat",
    async () => {
        return await combatStore.joinCombat().then(() => true);
    },
    { server: false },
);

// Modal State
type CombatPageModalType =
    | "Combat Opened Stage Characters"
    | "Combat Started Staged Character Menu"
    | "Edit Staged Character"
    | "Edit Initiative Character";
const combatPageModal = ref<InstanceType<typeof Modal> | null>(null);
const modalState = reactive<{
    open: boolean;
    modalType: CombatPageModalType | null;
    title: string | null;
}>({
    open: false,
    modalType: null,
    title: null,
});
function showModal(modalType: CombatPageModalType) {
    combatPageModal.value?.show();
    modalState.open = true;
    modalState.modalType = modalType;
    switch (modalType) {
        case "Combat Opened Stage Characters":
            modalState.title = "Stage Characters";
            break;
        case "Edit Staged Character":
            modalState.title = "Edit Staged Character";
            break;
        case "Combat Started Staged Character Menu":
            modalState.title = "Staged List";
            break;
        case "Edit Initiative Character":
            modalState.title = "Edit Character in Initiative";
            break;
    }
}
const lastClickedStagedCharacter = ref<StagedCharacter | undefined>(undefined);
const lastClickedInitiativeCharacter = ref<InitiativeCharacter | undefined>(
    undefined,
);
async function closeModal() {
    combatPageModal.value?.hide();
    modalState.open = false;
    modalState.title = null;
    lastClickedStagedCharacter.value = undefined;
    lastClickedInitiativeCharacter.value = undefined;
}

function onClickCharacter(character: StagedCharacter | InitiativeCharacter) {
    if (combatHasStarted.value) {
        lastClickedStagedCharacter.value = character as StagedCharacter;
        showModal("Edit Staged Character");
    } else if (combatInInitiative.value) {
        lastClickedInitiativeCharacter.value = character as InitiativeCharacter;
        showModal("Edit Initiative Character");
    }
}

async function onDeleteStagedCharacter(
    req: Omit<DeleteStagedCharacterRequest, "combatId">,
) {
    return await useCombatStore()
        .deleteStagedCharacter(req)
        .then(() => {
            closeModal();
        });
}

async function onUpsertStagedCharacter(req: StagedCharacterDTO) {
    return combatStore.updateStagedCharacter(req).then(closeModal);
}

async function onStagePlannedCharacters(
    req: PostStagePlannedCharactersRequest["plannedCharactersToStage"],
) {
    return combatStore.stagePlannedCharacters(req).then(closeModal);
}

const isUsersTurn = computed(() => {
    if (campaignStore.isDm) {
        return true;
    }

    if (combat.value?.initiativeIndex == null) {
        return false;
    }

    const characterWithCurrentInitiative =
        combat.value?.initiativeList[combat.value.initiativeIndex];
    if (
        characterWithCurrentInitiative?.playerId == userStore.state.user?.userId
    ) {
        return true;
    }
    return false;
});

// Navigation on finished combat
const goHome = async () => {
    await useNavigator().toCampaignTab(
        combatStore.state.combat?.campaignId!,
        "summary",
    );
};
</script>

<style>
.shuffleList-move, /* apply transition to moving elements */
.shuffleList-enter-active,
.shuffleList-leave-active {
    transition: all 1s ease-in-out;
}

.shuffleList-enter-from,
.shuffleList-leave-to {
    opacity: 0;
}

/* ensure leaving items are taken out of layout flow so that moving
   animations can be calculated correctly. */
.shuffleList-leave-active {
    position: absolute;
}

.slide-enter-active,
.slide-leave-active {
    transition: all 0.3s ease-in-out;
}

.slide-enter-from,
.slide-leave-to {
    transform: translateX(-100%);
    z-index: 19;
    opacity: 0;
}
</style>
