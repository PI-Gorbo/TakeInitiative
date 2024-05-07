<template>
    <div class="drawer drawer-end flex h-full w-full">
        <input
            ref="combatDrawer"
            id="combatDrawer"
            type="checkbox"
            class="drawer-toggle"
        />
        <div
            class="drawer-content flex h-full w-full flex-1 flex-col overflow-y-auto"
        >
            <!-- MAIN CONTENT -->
            <header class="grid grid-cols-3 p-2">
                <div>
                    <FormButton
                        v-if="combatIsStarted && isUsersTurn"
                        icon="arrows-rotate"
                        label="End Turn"
                        buttonColour="take-yellow"
                        hoverButtonColour="take-yellow-dark"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'End...',
                        }"
                        :click="combatStore.endTurn"
                    />
                </div>
                <label
                    class="flex items-center justify-center text-center font-NovaCut text-lg"
                    >{{ combat?.combatName }}</label
                >
                <div class="flex justify-end px-1">
                    <FormButton
                        v-if="!combatIsFinished"
                        icon="bars"
                        class="aspect-square w-min"
                        buttonColour="take-navy"
                        textColour="base-200"
                        hoverButtonColour="take-navy-dark"
                        :preventClickBubbling="false"
                        @clicked="toggleDrawer"
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
                        class="border-2 border-take-navy-light p-2"
                        @combatOpenedStageCharacters="
                            () => showModal('Combat Opened Stage Characters')
                        "
                        @OnClickCharacter="
                            (character) => onClickCharacter(character!)
                        "
                    />
                </section>
            </main>
        </div>
        <div class="drawer-side">
            <label
                for="combatDrawer"
                aria-label="close sidebar"
                class="drawer-overlay"
            ></label>
            <ul
                class="menu flex min-h-full w-80 flex-col gap-2 bg-take-navy p-4 text-base-content"
            >
                <!-- Sidebar content here -->
                <li>
                    <FormButton
                        label="Staged Characters"
                        icon="clipboard-question"
                        @clicked="
                            () => {
                                showModal(
                                    combatIsOpen
                                        ? 'Combat Opened Stage Characters'
                                        : 'Combat Started Staged Character Menu',
                                );
                                toggleDrawer();
                            }
                        "
                        buttonColour="take-navy-dark"
                        hoverButtonColour="take-navy-medium"
                        hoverTextColour="white"
                    />
                </li>
                <li>
                    <FormButton
                        v-if="combatIsOpen"
                        label="Start Combat"
                        icon="shoe-prints"
                        :click="
                            () => combatStore.startCombat().then(toggleDrawer)
                        "
                        buttonColour="take-navy-dark"
                        hoverButtonColour="take-navy-medium"
                        hoverTextColour="white"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'Starting...',
                        }"
                    />
                    <FormButton
                        v-else-if="combatIsStarted"
                        label="Finish Combat"
                        icon="flag-checkered"
                        :click="
                            () => combatStore.finishCombat().then(toggleDrawer)
                        "
                        buttonColour="take-navy-dark"
                        hoverButtonColour="take-navy-medium"
                        hoverTextColour="white"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'Finishing...',
                        }"
                    />
                </li>
            </ul>
        </div>
        <Modal ref="combatPageModal" :title="modalState.title ?? ''">
            <CombatStageCharacterForm
                v-if="modalState.modalType == 'Edit Staged Character'"
                :onEdit="(req) => onUpsertStagedCharacter(req).then(closeModal)"
                :onDelete="onDeleteStagedCharacter"
                :character="lastClickedStagedCharacter!"
            />
            <Tabs
                v-if="modalState.modalType == 'Combat Opened Stage Characters'"
                :showTabs="{
                    Planned: () => userIsDm,
                    Characters: () =>
                        (campaignStore.state.userCampaignMember?.characters
                            ?.length ?? 0) > 0,
                }"
                class="py-2"
                backgroundColour="take-navy-medium"
            >
                <template #Custom>
                    <CombatStageCharacterForm
                        :onCreate="onUpsertStagedCharacter"
                    />
                </template>
                <template #Planned>
                    <CombatPlannedStagesDisplay
                        :stages="combatStore.state.combat?.plannedStages!"
                        :submit="onStagePlannedCharacters"
                    />
                </template>
            </Tabs>
            <main
                v-if="
                    modalState.modalType ==
                    'Combat Started Staged Character Menu'
                "
            >
                <CombatStagedCharactersSection
                    @RolledCharactersIntoInitiative="() => closeModal()"
                />
            </main>
        </Modal>
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type CollapsableHorizontalMenu from "~/components/CollapsableHorizontalMenu.vue";
import ConfirmButton from "~/components/Form/ConfirmButton.vue";
import Modal from "~/components/Modal.vue";
import type { CampaignMemberDto } from "~/utils/api/campaign/getCampaignRequest";
import type { DeleteStagedCharacterRequest } from "~/utils/api/combat/deleteStagedCharacterRequest";
import type { PostStagePlannedCharactersRequest } from "~/utils/api/combat/postStagePlannedCharactersRequest";
import type {
    StagedCharacterDTO,
    UpsertStagedCharacterRequest,
} from "~/utils/api/combat/putUpsertStagedCharacter";
import {
    CombatState,
    type Combat,
    type CombatCharacter,
} from "~/utils/types/models";

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
const { combat, userIsDm, combatIsOpen, combatIsStarted, combatIsFinished } =
    storeToRefs(combatStore);

// Main fetch
const { refresh, pending, error } = await useAsyncData("Combat", async () => {
    return await combatStore.setCombat(combatId as string).then(() => true);
});

// Ensures the user is joined whenever the page loads.
const {} = await useAsyncData(
    "JoinCombat",
    async () => {
        console.log("Sending join request");
        return await combatStore.joinCombat().then(() => true);
    },
    { server: false },
);

// Modal State
type CombatPageModalType =
    | "Combat Opened Stage Characters"
    | "Combat Started Staged Character Menu"
    | "Edit Staged Character";
const combatPageModal = ref<typeof Modal | null>(null);
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
            modalState.title = "Staged Characters";
    }
}
function closeModal() {
    combatPageModal.value?.hide();
    modalState.open = false;
    modalState.title = null;
}

// // Combat logs
// const combatLogs = ref<HTMLDivElement | null>(null);
// watch(
//     () => combat.value?.combatLogs,
//     (before, after) => {
//         if (!combatLogs.value) return;
//         combatLogs.value.scrollTop = 0;
//     },
// );
// const prettyCombatLogs = computed(() =>
//     combat.value?.combatLogs.map((log) => `> ${log}`).toReversed(),
// );

// Displaying the character list,

const lastClickedStagedCharacter = ref<CombatCharacter | undefined>(undefined);
function onClickCharacter(character: CombatCharacter) {
    if (combat.value?.state == CombatState.Open) {
        lastClickedStagedCharacter.value = character;
        showModal("Edit Staged Character");
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
    return combatStore.upsertStagedCharacter(req).then(closeModal);
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

    if (!combat.value?.initiativeIndex) {
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
