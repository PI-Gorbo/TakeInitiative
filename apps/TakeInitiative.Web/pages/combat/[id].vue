<template>
    <div class="flex h-full w-full">
        <div :class="['h-full w-full flex-1']">
            <!-- MAIN CONTENT -->
            <Transition name="fade">
                <main
                    class="relative flex h-full flex-1 flex-col items-center justify-center px-4"
                >
                    <CollapsableHorizontalMenu
                        ref="horizontalMenu"
                        :menuItems="[
                            {
                                label: 'Staged Characters',
                                hasNotificationIcon: false,
                            },
                        ]"
                        @menuItemClicked="
                            (item) => {
                                if (item.label == 'Staged Characters') {
                                    showModal(
                                        'Combat Started Staged Character Menu',
                                    );
                                    horizontalMenu!.hide();
                                }
                            }
                        "
                    >
                        <template #Left>
                            <a
                                class="flex-none cursor-default select-none text-xl font-semibold"
                                href="#"
                                >{{ combat?.combatName }}</a
                            >
                        </template>
                    </CollapsableHorizontalMenu>
                    <!-- <header class="grid w-full max-w-[1200px] grid-cols-3 py-2">
                        <div
                            class="flex items-center gap-2"
                            v-if="combatIsStarted"
                        >
                            <FormButton
                                class="px-2"
                                label="End Turn"
                                size="sm"
                                :click="combatStore.endTurn"
                                :loadingDisplay="{
                                    showSpinner: true,
                                    loadingText: 'Ending Turn',
                                }"
                                :disabled="!isUsersTurn"
                            /> -->
                    <!-- <FormButton
                                buttonColour="take-navy-light"
                                class="px-2"
                                label="View Staged Characters"
                                size="sm"
                                :disabled="!isUsersTurn"
                                @clicked="
                                    () =>
                                        showModal(
                                            'Combat Started Staged Character Menu',
                                        )
                                "
                            /> -->
                    <!-- <label class="px-2">
                                Round: {{ combat?.roundNumber }}
                            </label> -->
                    <!-- </div>
                        <div class="flex items-center" v-if="combatIsOpen">
                            <FormButton
                                class="px-2"
                                label="Stage Characters"
                                size="sm"
                                :click="
                                    async () =>
                                        showModal(
                                            'Combat Opened Stage Characters',
                                        )
                                "
                                :disabled="!isUsersTurn"
                            />
                        </div>
                        <label
                            class="col-start-2 flex items-center justify-center text-center font-NovaCut text-xl text-take-yellow"
                            >{{ combat?.combatName }}</label
                        >
                        <div class="flex justify-end">
                            <ConfirmButton
                                v-if="userIsDm && combatIsOpen"
                                label="Start Combat"
                                :loadingDisplay="{
                                    showSpinner: true,
                                    loadingText: 'Starting',
                                }"
                                size="sm"
                                :click="combatStore.startCombat"
                            />
                            <ConfirmButton
                                v-if="userIsDm && combatIsStarted"
                                label="Finish Combat"
                                confirmText="Confirm Finish"
                                :loadingDisplay="{
                                    showSpinner: true,
                                    loadingText: 'Finishing',
                                }"
                                confirmHoverColour="take-red"
                                size="sm"
                                buttonColour="take-navy-light"
                                m
                                :click="combatStore.finishCombat"
                            />
                        </div>
                    </header> -->
                    <main
                        :class="[
                            'flex h-full w-full max-w-[1200px] flex-1 flex-row justify-center overflow-y-auto pb-2',
                        ]"
                    >
                        <section
                            class="flex h-full flex-1 flex-col gap-2 overflow-y-auto"
                        >
                            <div class="flex-1">
                                <CombatCharacterListSection
                                    class="border-2 border-take-navy-light p-2"
                                    @combatOpenedStageCharacters="
                                        () =>
                                            showModal(
                                                'Combat Started Staged Character Menu',
                                            )
                                    "
                                    @OnClickCharacter="
                                        (character) =>
                                            onClickCharacter(character!)
                                    "
                                />
                            </div>
                        </section>
                    </main>
                </main>
            </Transition>
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
    layout: "index-page",
});

// Route
const route = useRoute();
const combatId = route.params.id;

// Combat data
const combatStore = useCombatStore();
const { combat, userIsDm } = storeToRefs(combatStore);

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
const horizontalMenu = ref<typeof CollapsableHorizontalMenu | null>(null);
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

    const characterWithCurrentInitiative =
        combat.value?.initiativeList[combat.value.initiativeIndex ?? 0];
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
