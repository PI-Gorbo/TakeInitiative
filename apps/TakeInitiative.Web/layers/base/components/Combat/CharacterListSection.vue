<template>
    <TransitionGroup
        class="flex h-full flex-1 select-none flex-col gap-2 overflow-y-auto rounded-lg"
        tag="ul"
        name="shuffleList"
    >
        <!-- INITIATIVE LIST -->
        <li
            v-for="{ char: charInfo, index } in characterList
                .map((char, index) => ({ char, index }))
                .filter((x) => userIsDm || !x.char.character.hidden)"
            :key="charInfo.character.id"
            :class="[
                'flex flex-col gap-2 rounded-xl border-2 border-take-purple-light p-2 transition-colors',
                {
                    'cursor-pointer':
                        (combatIsOpen || combatIsStarted) &&
                        isEditableForUser(charInfo),
                    ' hover:border-take-yellow':
                        (combatIsOpen || combatIsStarted) &&
                        isEditableForUser(charInfo),
                    'border-take-red':
                        props.listToDisplay != 'Staging' &&
                        combatIsStarted &&
                        index == combat?.initiativeIndex,
                },
            ]"
            @click="
                () =>
                    isEditableForUser(charInfo)
                        ? emit('OnClickCharacter', charInfo.character)
                        : null
            "
        >
            <main
                class="flex flex-wrap items-center justify-between gap-2 text-white"
            >
                <!-- Initiative value when combat initiative has been rolled -->
                <aside class="flex items-center gap-2">
                    <section v-if="!combatIsOpen" class="flex gap-2">
                        <div
                            v-if="isInitiativeCharacter(charInfo.character)"
                            v-for="(value, index) in charInfo.character
                                .initiative.value"
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
                    </section>
                    <section class="flex gap-2">
                        <!-- Username -->
                        <span
                            :class="[
                                {
                                    'cursor-pointer':
                                        isEditableForUser(charInfo) &&
                                        (combatIsOpen ||
                                            combatIsStarted ||
                                            props.listToDisplay == 'Staging'),
                                },
                            ]"
                        >
                            <FontAwesomeIcon
                                class="text-take-yellow"
                                :icon="combatStore.getIconForUser(charInfo)"
                            />
                            {{ charInfo.user?.username }}
                        </span>
                        <!-- Character Name -->
                        <span>
                            {{ charInfo.character.name }}
                        </span>
                    </section>
                </aside>

                <aside class="flex flex-wrap gap-2">
                    <section
                        v-if="
                            userIsDm &&
                            charInfo.character.playerId ==
                                userStore.state.user?.userId
                        "
                        class="rounded bg-take-purple p-1"
                    >
                        <FontAwesomeIcon
                            :icon="
                                charInfo.character.hidden ? 'eye-slash' : 'eye'
                            "
                        />
                    </section>

                    <CharacterHealthDisplay
                        :health="charInfo.character.health"
                        :displayMethod="getHealthDisplayMethod(charInfo)"
                        class="min-w-max rounded bg-take-purple p-1"
                    />

                    <CharacterArmourClassDisplay
                        :armourClass="charInfo.character.armourClass"
                        :armourClassDisplayMethod="
                            getArmourClassDisplayMethod(charInfo)
                        "
                        class="flex items-center justify-center gap-1 rounded bg-take-purple p-1"
                    />

                    <section
                        v-if="
                            combatIsOpen &&
                            isStagedCharacter(charInfo.character)
                        "
                        class="flex items-center justify-center gap-1 rounded bg-take-purple p-1"
                    >
                        <FontAwesomeIcon icon="shoe-prints" />
                        {{ charInfo.character.initiative.roll }}
                    </section>

                    <li
                        v-if="isInitiativeCharacter(charInfo.character)"
                        v-for="(
                            condition, index
                        ) in charInfo.character.conditions.toSorted((a, b) =>
                            a.name > b.name ? 1 : -1,
                        )"
                        :key="index"
                        :class="[
                            `bg-${getConditionBackgroundColour(condition.name)} text-${TakeInitContrastColour[getConditionBackgroundColour(condition.name)]} flex items-center gap-1 rounded p-1`,
                        ]"
                    >
                        <FontAwesomeIcon
                            :icon="getConditionIcon(condition.name)"
                        />
                        {{ condition.name }}
                    </li>
                </aside>
            </main>
        </li>

        <!-- When there are no characters displayed on the user, show a message -->
        <li
            v-if="
                combatIsStarted &&
                characterList
                    .map((char, index) => ({ char, index }))
                    .filter((x) => userIsDm || !x.char.character.hidden)
                    .length == 0
            "
        >
            <label
                class="flex justify-center text-take-grey"
                v-if="characterList.find((x) => x.character.hidden)"
            >
                The DM has one or more hidden characters that they are waiting
                to reveal.
            </label>
            <label class="flex justify-center text-take-grey" v-else>
                There are no characters left in this combat!
            </label>
        </li>
        <li
            key="footer"
            v-if="combatIsOpen"
            :class="[
                'cursor-pointer rounded-xl border-2 border-dashed border-take-navy-light p-2 text-center transition-colors hover:border-take-yellow',
            ]"
            @click="emit('CombatOpenedStageCharacters')"
        >
            + Stage Characters
        </li>
    </TransitionGroup>
</template>
<script setup lang="ts">
import {
    type InitiativeCharacter,
    CombatState,
    type HealthDisplayOptionValues,
    HealthDisplayOptionsEnum,
    type ArmourClassDisplayOptionValues,
    ArmourClassDisplayOptionValueKeyMap,
    ArmourClassDisplayOptionsEnum,
    type StagedCharacter,
} from "base/utils/types/models";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import { TakeInitContrastColour } from "base/utils/types/HelperTypes";

const userStore = useUserStore();
const campaignStore = useCampaignStore();
const combatStore = useCombatStore();
const combat = computed(() => {
    return combatStore.state.combat;
});
const {
    combatIsOpen,
    combatIsStarted,
    combatIsFinished,
    orderedStagedCharacterListWithPlayerInfo,
    initiativeListWithPlayerInfo,
    userIsDm,
} = storeToRefs(combatStore);
const isEditableForUser = combatStore.isEditableForUser;

const props = withDefaults(
    defineProps<{
        listToDisplay?: "Staging" | "Initiative" | undefined;
    }>(),
    {},
);

const emit = defineEmits<{
    (e: "CombatOpenedStageCharacters"): void;
    (
        e: "OnClickCharacter",
        character: InitiativeCharacter | StagedCharacter,
    ): void;
}>();

const combatHasHiddenCharacters = computed(
    () => characterList.value.find((x) => x.character.hidden) != null,
);

const characterList: ComputedRef<(StagedPlayerDto | InitiativePlayerDto)[]> =
    computed(() => {
        if (props.listToDisplay == undefined) {
            if (combat.value?.state == CombatState.Open) {
                return orderedStagedCharacterListWithPlayerInfo.value;
            } else {
                return initiativeListWithPlayerInfo.value;
            }
        } else if (props.listToDisplay == "Staging") {
            return orderedStagedCharacterListWithPlayerInfo.value;
        } else {
            return initiativeListWithPlayerInfo.value;
        }
    });

function getHealthDisplayMethod(
    character: StagedPlayerDto | InitiativePlayerDto,
): HealthDisplayOptionValues {
    if (userIsDm.value) {
        return HealthDisplayOptionsEnum["RealValue"];
    }

    if (character.user.isDungeonMaster) {
        return campaignStore.state.campaign?.campaignSettings
            .combatHealthDisplaySettings.dmCharacterDisplayMethod!;
    }

    return campaignStore.state.campaign?.campaignSettings
        .combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod!;
}

function getArmourClassDisplayMethod(
    character: StagedPlayerDto | InitiativePlayerDto,
): ArmourClassDisplayOptionValues {
    if (userIsDm.value) {
        return ArmourClassDisplayOptionsEnum.RealValue;
    }

    if (character.user.isDungeonMaster) {
        return campaignStore.state.campaign?.campaignSettings
            .combatArmourClassDisplaySettings.dmCharacterDisplayMethod!;
    }

    return campaignStore.state.campaign?.campaignSettings
        .combatArmourClassDisplaySettings.otherPlayerCharacterDisplayMethod!;
}

function isInitiativeCharacter(
    character: InitiativeCharacter | StagedCharacter,
): character is InitiativeCharacter {
    return (character as InitiativeCharacter).initiative.value !== undefined;
}

function isStagedCharacter(
    character: InitiativeCharacter | StagedCharacter,
): character is StagedCharacter {
    return (character as StagedCharacter).initiative.roll !== undefined;
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
</style>
