<template>
    <div class="flex h-full w-full flex-col items-center">
        <header class="grid w-full max-w-[1200px] grid-cols-3 py-2">
            <div class="flex items-center" v-if="combatIsStarted">
                <FormButton
                    class="px-2"
                    label="End Turn"
                    size="sm"
                    :click="combatStore.endTurn"
                    :disabled="!isUsersTurn"
                />
                <label class="px-2"> Round: {{ combat?.roundNumber }} </label>
                <label class="px-2">
                    Current Initiative: {{ combat?.initiativeIndex }}
                </label>
            </div>
            <div class="flex items-center" v-else-if="combatIsOpen">
                <FormButton
                    class="px-2"
                    label="Stage Character(s)"
                    size="sm"
                    buttonColour="take-navy-light"
                    @clicked="onShowNewStagedCharacterModal"
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
                        loadingText: 'Starting...',
                    }"
                    size="sm"
                    :click="combatStore.startCombat"
                />

                <ConfirmButton
                    v-if="userIsDm && combatIsStarted"
                    label="Finish Combat"
                    confirmText="Confirm Finish"
                    confirmHoverColour="take-red"
                    size="sm"
                    buttonColour="take-navy-light"
                    :click="combatStore.finishCombat"
                />
            </div>
        </header>
        <main
            :class="['flex h-full w-full max-w-[1200px] flex-1 flex-row justify-center']"
        >
            <section class="flex h-full flex-1 flex-col gap-2 overflow-y-auto">
                <div class="flex-1">
                    <TransitionGroup
                        class="flex h-full flex-1 select-none flex-col gap-2 rounded-lg border-2 border-take-navy-light p-2"
                        tag="ul"
                        name="shuffleList"
                    >
                        <li
                            v-for="(charInfo, index) in characterList"
                            :key="charInfo.character.id"
                            :class="[
                                'grid grid-cols-2 rounded-xl border-2 border-take-navy-light p-2 transition-colors',
                                {
                                    'cursor-pointer hover:border-take-yellow':
                                        isEditableForUser(charInfo) && combatIsOpen,
                                    'border-take-yellow':
                                        combatIsStarted &&
                                        index == combat?.initiativeIndex,
                                },
                            ]"
                            @click="
                                () =>
                                    isEditableForUser(charInfo)
                                        ? onClickCharacter(charInfo.character)
                                        : null
                            "
                        >
                            <header class="flex items-center gap-2">
                                <div
                                    v-if="!combatIsOpen"
                                    :class="[
                                        {
                                            'cursor-pointer':
                                                isEditableForUser(charInfo) &&
                                                combatIsOpen,
                                        },
                                        'flex gap-2',
                                    ]"
                                >
                                    <div
                                        v-for="(value, index) in charInfo.character
                                            .initiativeValue"
                                        :class="[
                                            'flex items-center rounded-lg  p-1',
                                            {
                                                'text-xs': index != 0,
                                                'bg-take-navy-light': index == 0,
                                                'bg-take-navy-medium': index != 0,
                                            },
                                        ]"
                                    >
                                        {{ value }}
                                    </div>
                                </div>
                                <label
                                    :class="[
                                        {
                                            'cursor-pointer':
                                                isEditableForUser(charInfo) &&
                                                combatIsOpen,
                                        },
                                    ]"
                                >
                                    <FontAwesomeIcon
                                        class="text-take-yellow"
                                        :icon="getIconForUser(charInfo)"
                                    />
                                    {{ charInfo.user?.username }}:</label
                                >
                                <label
                                    :class="[
                                        {
                                            'cursor-pointer':
                                                isEditableForUser(charInfo) &&
                                                combatIsOpen,
                                        },
                                    ]"
                                    >{{ charInfo.character.name }}</label
                                >
                            </header>
                            <body>
                                <ol class="flex flex-row justify-end">
                                    <li v-if="combatIsOpen">
                                        <FontAwesomeIcon icon="shoe-prints" />
                                        {{ charInfo.character.initiative.value }}
                                    </li>
                                </ol>
                            </body>
                        </li>
                        <li v-if="combatIsOpen">
                            <div
                                :class="[
                                    'group flex w-full cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-take-navy-light transition-colors hover:border-take-yellow',
                                ]"
                                @click="onShowNewStagedCharacterModal"
                            >
                                <FormButton
                                    class="group-hover:text-take-yellow"
                                    buttonColour="take-navy"
                                    hoverButtonColour="take-navy"
                                    textColour="take-grey"
                                    hoverTextColour="take-yellow"
                                    icon="plus"
                                    label="Stage character(s)"
                                    size="sm"
                                    :preventClickBubbling="false"
                                />
                            </div>
                        </li>
                    </TransitionGroup>
                    <Modal
                        ref="stageNewCharacterModal"
                        title="Stage your Characters"
                        class="max-w-[1200px] w-full"
                    >
                        <Tabs
                            :showTabs="{
                                Planned: () => userIsDm,
                                Characters: () =>
                                    (campaignStore.state.userCampaignMember?.characters
                                        ?.length ?? 0) > 0,
                            }"
                            class="overflow-auto"
                        >
                            <template #Planned>
                                <CombatPlannedStagesDisplay
                                    :stages="combatStore.state.combat?.plannedStages!"
                                />
                            </template>
                            <!-- <template #Characters>
                                <CombatStageCharacterForm
                                    :onCreate="onUpsertStagedCharacter"
                                />
                            </template> -->
                            <template #Custom>
                                <CombatStageCharacterForm
                                    :onCreate="onUpsertStagedCharacter"
                                />
                            </template>
                        </Tabs>
                    </Modal>
                    <Modal ref="editStagedCharacterModal" title="Edit staged Character">
                        <CombatStageCharacterForm
                            :onEdit="onUpsertStagedCharacter"
                            :onDelete="onDeleteStagedCharacter"
                            :character="lastClickedStagedCharacter!"
                        />
                    </Modal>
                </div>
            </section>
        </main>
        <footer class="flex w-full max-w-[1200px] flex-col py-2">
            <textarea
                class="w-full overflow-y-auto rounded-lg bg-take-navy-medium p-2"
                ref="combatLogs"
                :value="joinedCombatLogs"
                rows="5"
                cols="50"
            />
        </footer>
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import ConfirmButton from "~/components/Form/ConfirmButton.vue";
import Modal from "~/components/Modal.vue";
import type { CampaignMemberDto } from "~/utils/api/campaign/getCampaignRequest";
import type { DeleteStagedCharacterRequest } from "~/utils/api/combat/deleteStagedCharacterRequest";
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
});

// Route
const route = useRoute();
const combatId = route.params.id;

// Combat data
const combatStore = useCombatStore();
const combat = computed(() => {
    return combatStore.state.combat;
});
const userIsDm = computed(
    () => userStore.state.user?.userId == combat.value?.dungeonMaster,
);
const combatIsOpen = computed(() => combat.value?.state == CombatState.Open);
const combatIsStarted = computed(
    () => combat.value?.state == CombatState.Started,
);
const combatIsFinished = computed(
    () => combat.value?.state == CombatState.Finished,
);

// Main fetch
const { refresh, pending, error } = await useAsyncData("Combat", async () => {
    return await combatStore.setCombat(combatId as string).then(() => true);
});

// Main fetch
const {} = await useAsyncData(
    "JoinCombat",
    async () => {
        console.log("Sending join request");
        return await combatStore.joinCombat().then(() => true);
    },
    { server: false },
);

// Combat logs
const combatLogs = ref<HTMLDivElement | null>(null);
watch(
    () => combat.value?.combatLogs,
    (before, after) => {
        if (!combatLogs.value) return;
        combatLogs.value.scrollTop = 0;
    },
);
const joinedCombatLogs = computed(() =>
    combat.value?.combatLogs
        .reverse()
        .map((log) => `> ${log}`)
        .join("\n"),
);

type PlayerDto = {
    user: CampaignMemberDto;
    character: CombatCharacter;
};

const characterList = computed(() => {
    const compareStrings = (a: string, b: string) => {
        let fa = a.toLowerCase(),
            fb = b.toLowerCase();

        if (fa < fb) {
            return -1;
        }
        if (fa > fb) {
            return 1;
        }
        return 0;
    };

    const openCombatCharacterSortFunc = (
        a: PlayerDto,
        b: PlayerDto,
    ): number => {
        const aIsDungeonMaster = a.user?.isDungeonMaster;
        const bIsDungeonMaster = b.user?.isDungeonMaster;
        if (aIsDungeonMaster && !bIsDungeonMaster) {
            return -1;
        } else if (!aIsDungeonMaster && bIsDungeonMaster) {
            return 1;
        }

        // First sort by user,
        let result = compareStrings(a.user?.username!, b.user?.username!);
        if (result != 0) {
            return result;
        }

        // Then sort by character id
        result = compareStrings(a.character.id, b.character.id);
        return result;
    };

    const list =
        combat.value?.state == CombatState.Open
            ? combat.value.stagedList
            : combat.value?.initiativeList;
    const sortFunc =
        combat.value?.state == CombatState.Open
            ? openCombatCharacterSortFunc
            : null;

    const playerDTOs = list!.map(
        (x) =>
            ({
                user: campaignStore.getMemberDetailsFor(x.playerId)!,
                character: x,
            }) satisfies PlayerDto,
    );

    if (sortFunc) {
        return playerDTOs.sort(sortFunc);
    }

    return playerDTOs;
});

const editStagedCharacterModal = ref<typeof Modal | null>(null);
const lastClickedStagedCharacter = ref<CombatCharacter | undefined>(undefined);
function onClickCharacter(character: CombatCharacter) {
    if (combat.value?.state == CombatState.Open) {
        lastClickedStagedCharacter.value = character;
        editStagedCharacterModal.value?.show();
    }
}

const stageNewCharacterModal = ref<typeof Modal | null>(null);
function onShowNewStagedCharacterModal() {
    stageNewCharacterModal.value?.show();
}

async function onDeleteStagedCharacter(
    req: Omit<DeleteStagedCharacterRequest, "combatId">,
) {
    return await useCombatStore()
        .deleteStagedCharacter(req)
        .then(() => {
            editStagedCharacterModal.value?.hide();
        });
}

async function onUpsertStagedCharacter(req: StagedCharacterDTO) {
    return combatStore.upsertStagedCharacter(req).then(() => {
        stageNewCharacterModal.value?.hide();
        editStagedCharacterModal.value?.hide();
    });
}

// Get Icon depending on user
function getIconForUser(charInfo: {
    user: CampaignMemberDto;
    character: CombatCharacter;
}) {
    const currentUserId = userStore.state.user?.userId;

    if (charInfo.user.userId == combat.value?.dungeonMaster) {
        return "crown";
    }

    if (charInfo.user.userId == currentUserId) {
        return "circle-user";
    }

    return "user-large";
}

// Determines if the current row is editable for this current user
function isEditableForUser(charInfo: {
    user: CampaignMemberDto;
    character: CombatCharacter;
}) {
    return (
        userStore.state.user?.userId == combat?.value?.dungeonMaster ||
        charInfo.user?.userId == userStore.state.user?.userId
    );
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
</style>
