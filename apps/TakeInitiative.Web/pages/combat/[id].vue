<template>
    <div class="flex h-full w-full">
        <!-- SIDE PANEL -->
        <aside class="flex w-min">
            <ol
                class="gal-2 z-50 flex flex-col divide-y-2 divide-take-navy border-r border-take-navy bg-take-navy-medium p-2"
            >
                <CombatAsidePanelMenuItem
                    :highlighted="asidePanelState.panelName == 'Logs'"
                    icon="receipt"
                    label="Logs"
                    @click="() => onClickPanel('Logs')"
                />
                <CombatAsidePanelMenuItem
                    :highlighted="asidePanelState.panelName == 'Roll'"
                    icon="dice-d20"
                    label="Roll"
                    @click="() => onClickPanel('Roll')"
                />
                <CombatAsidePanelMenuItem
                    v-if="!combatIsFinished"
                    :highlighted="
                        asidePanelState.panelName == 'Stage Characters'
                    "
                    icon="person-circle-plus"
                    label="Stage Characters"
                    @click="() => onClickPanel('Stage Characters')"
                />
                <CombatAsidePanelMenuItem
                    v-if="combatIsStarted && userIsDm"
                    :highlighted="asidePanelState.panelName == 'Add Characters'"
                    icon="person-circle-plus"
                    label="Add Characters"
                    @click="() => onClickPanel('Add Characters')"
                />
                <CombatAsidePanelMenuItem
                    v-if="combatIsStarted"
                    :highlighted="
                        asidePanelState.panelName == 'Staged Character List'
                    "
                    icon="list"
                    label="Staged Character List"
                    @click="() => onClickPanel('Staged Character List')"
                />
            </ol>
        </aside>

        <!-- REST OF THE PAGE -->
        <div
            :class="[
                'h-full w-full flex-1',
                isMediumScreenOrLarger ? '' : 'overflow-hidden',
            ]"
        >
            <!-- SLIDING SIDE PANEL SECTION -->
            <Transition
                name="slide"
                tag="div"
                :class="[
                    'z-40 h-full flex-1',
                    isMediumScreenOrLarger ? 'fixed' : '',
                ]"
            >
                <TransitionGroup
                    name="fade"
                    v-if="asidePanelState.open"
                    class="max-h-svh w-full overflow-y-auto bg-take-navy-medium px-2 md:w-[40ex]"
                >
                    <section
                        v-if="asidePanelState.panelName == 'Logs'"
                        key="Logs"
                        class="flex w-full flex-col gap-1 divide-y-2 divide-take-navy overflow-y-auto p-2 text-sm text-gray-200"
                    >
                        <p v-for="line in prettyCombatLogs">{{ line }}</p>
                    </section>
                    <section
                        v-if="asidePanelState.panelName == 'Roll'"
                        key="logs"
                        class="flex w-full flex-col gap-1 px-2 py-4 text-sm text-gray-200"
                    >
                        <section>
                            <FormBase
                                class="border-take-gray flex w-full gap-2 rounded-lg border border-opacity-20 p-2"
                                v-slot="{ submitting }"
                                :onSubmit="async () => {}"
                            >
                                <FormInput label="Custom Roll" class="flex-1" />
                                <FormButton
                                    label="Roll"
                                    icon="dice-d20"
                                    size="sm"
                                    class="items-center"
                                    buttonColour="take-grey-dark"
                                    textColour="take-navy"
                                    :isLoading="submitting"
                                />
                            </FormBase>
                            <a
                                target="_blank"
                                href="https://d20.readthedocs.io/en/latest/start.html#dice-syntax"
                                class="underline"
                            >
                                Custom roll syntax examples
                            </a>
                        </section>
                    </section>
                    <section
                        v-if="asidePanelState.panelName == 'Stage Characters'"
                        key="Stage"
                        class="flex w-full flex-col gap-1 text-sm text-gray-200"
                    >
                        <Tabs
                            :showTabs="{
                                Planned: () => userIsDm,
                                Characters: () =>
                                    (campaignStore.state.userCampaignMember
                                        ?.characters?.length ?? 0) > 0,
                            }"
                            class="py-2"
                            backgroundColour="take-navy-medium"
                        >
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
                            <template #Planned>
                                <CombatPlannedStagesDisplay
                                    :stages="
                                        combatStore.state.combat?.plannedStages!
                                    "
                                    :submit="onStagePlannedCharacters"
                                />
                            </template>
                        </Tabs>
                    </section>
                </TransitionGroup>
            </Transition>

            <!-- MAIN CONTENT -->
            <Transition name="fade">
                <main
                    class="relative flex h-full flex-1 flex-col items-center justify-center px-4"
                    v-if="showMainPanel"
                >
                    <header class="grid w-full max-w-[1200px] grid-cols-3 py-2">
                        <div class="flex items-center" v-if="combatIsStarted">
                            <FormButton
                                class="px-2"
                                label="End Turn"
                                size="sm"
                                :click="combatStore.endTurn"
                                :disabled="!isUsersTurn"
                            />
                            <label class="px-2">
                                Round: {{ combat?.roundNumber }}
                            </label>
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
                        :class="[
                            'flex h-full w-full max-w-[1200px] flex-1 flex-row justify-center overflow-y-auto',
                        ]"
                    >
                        <section
                            class="flex h-full flex-1 flex-col gap-2 overflow-y-auto"
                        >
                            <div class="flex-1">
                                <TransitionGroup
                                    class="flex h-full flex-1 select-none flex-col gap-2 rounded-lg border-2 border-take-navy-light p-2"
                                    tag="ul"
                                    name="shuffleList"
                                >
                                    <!-- INITIATIVE LIST -->

                                    <li
                                        v-for="(
                                            charInfo, index
                                        ) in characterList"
                                        :key="charInfo.character.id"
                                        :class="[
                                            'grid grid-cols-2 rounded-xl border-2 border-take-navy-light p-2 transition-colors',
                                            {
                                                'cursor-pointer hover:border-take-yellow':
                                                    isEditableForUser(
                                                        charInfo,
                                                    ) && combatIsOpen,
                                                'border-take-yellow':
                                                    combatIsStarted &&
                                                    index ==
                                                        combat?.initiativeIndex,
                                            },
                                        ]"
                                        @click="
                                            () =>
                                                isEditableForUser(charInfo)
                                                    ? onClickCharacter(
                                                          charInfo.character,
                                                      )
                                                    : null
                                        "
                                    >
                                        <header class="flex items-center gap-2">
                                            <!-- Initiative -->
                                            <div
                                                v-if="!combatIsOpen"
                                                :class="[
                                                    {
                                                        'cursor-pointer':
                                                            isEditableForUser(
                                                                charInfo,
                                                            ) && combatIsOpen,
                                                    },
                                                    'flex gap-2',
                                                ]"
                                            >
                                                <div
                                                    v-for="(
                                                        value, index
                                                    ) in charInfo.character
                                                        .initiativeValue"
                                                    :class="[
                                                        'flex items-center rounded-lg  p-1',
                                                        {
                                                            'text-xs':
                                                                index != 0,
                                                            'bg-take-navy-light':
                                                                index == 0,
                                                            'bg-take-navy-medium':
                                                                index != 0,
                                                        },
                                                    ]"
                                                >
                                                    {{ value }}
                                                </div>
                                            </div>
                                            <!-- Username -->
                                            <label
                                                :class="[
                                                    {
                                                        'cursor-pointer':
                                                            isEditableForUser(
                                                                charInfo,
                                                            ) && combatIsOpen,
                                                    },
                                                ]"
                                            >
                                                <FontAwesomeIcon
                                                    class="text-take-yellow"
                                                    :icon="
                                                        getIconForUser(charInfo)
                                                    "
                                                />
                                                {{
                                                    charInfo.user?.username
                                                }}:</label
                                            >
                                            <!-- Character Name -->
                                            <label
                                                :class="[
                                                    {
                                                        'cursor-pointer':
                                                            isEditableForUser(
                                                                charInfo,
                                                            ) && combatIsOpen,
                                                    },
                                                ]"
                                                >{{ charInfo.character.name }}
                                                {{
                                                    charInfo.character
                                                        .copyNumber != null
                                                        ? `(${charInfo.character.copyNumber})`
                                                        : ""
                                                }}</label
                                            >
                                        </header>
                                        <body>
                                            <ol
                                                class="flex flex-row justify-end"
                                            >
                                                <li v-if="combatIsOpen">
                                                    <FontAwesomeIcon
                                                        icon="shoe-prints"
                                                    />
                                                    {{
                                                        charInfo.character
                                                            .initiative.value
                                                    }}
                                                </li>
                                            </ol>
                                        </body>
                                    </li>
                                </TransitionGroup>
                                <Modal
                                    ref="editStagedCharacterModal"
                                    title="Edit staged Character"
                                >
                                    <CombatStageCharacterForm
                                        :onEdit="
                                            (req) =>
                                                onUpsertStagedCharacter().then(
                                                    () =>
                                                        editStagedCharacterModal.hide(),
                                                )
                                        "
                                        :onDelete="onDeleteStagedCharacter"
                                        :character="lastClickedStagedCharacter!"
                                    />
                                </Modal>
                            </div>
                        </section>
                    </main>
                </main>
            </Transition>
        </div>
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
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

// Ensures the user is joined whenever the page loads.
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
const prettyCombatLogs = computed(() =>
    combat.value?.combatLogs.map((log) => `> ${log}`).toReversed(),
);

// Displaying the character list,\
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

        // Then sort by character name
        result = compareStrings(a.character.name, b.character.name);
        if (result != 0) {
            return result;
        }

        // Sort by copy number
        result =
            (a.character.copyNumber ?? 0) < (b.character.copyNumber ?? 0)
                ? -1
                : 1;

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
        if (!isMediumScreenOrLarger) closeAsidePanel();
    });
}

async function onStagePlannedCharacters(
    req: PostStagePlannedCharactersRequest["plannedCharactersToStage"],
) {
    return combatStore.stagePlannedCharacters(req).then(() => {
        if (!isMediumScreenOrLarger) closeAsidePanel();
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

// Aside Panel State
type PanelName =
    | "Logs"
    | "Roll"
    | "Stage Characters"
    | "Staged Character List"
    | "Add Characters";
const asidePanelState = reactive<{
    open: boolean;
    panelName: PanelName | null;
}>({
    open: false,
    panelName: null,
});

const isMediumScreenOrLarger = computed(() => {
    if (process.client) {
        return window.matchMedia("(min-width: 768px)").matches;
    }
    return true;
});
const showMainPanel = computed(
    () => !(!isMediumScreenOrLarger && asidePanelState.open),
);

function onClickPanel(name: PanelName) {
    if (name == asidePanelState.panelName) {
        asidePanelState.open = false;
        asidePanelState.panelName = null;
    } else {
        asidePanelState.open = true;
        asidePanelState.panelName = name;
    }
}

function closeAsidePanel() {
    asidePanelState.open = false;
    asidePanelState.panelName = null;
}
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
