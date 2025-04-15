<template>
    <TransitionGroup
        class="flex select-none flex-col gap-2 overflow-y-auto rounded-lg"
        tag="article"
        name="shuffleList">
        <!-- INITIATIVE LIST -->
        <li
            v-for="{ char: charInfo, index } in characterList
                .map((char, index) => ({ char, index }))
                .filter((x) => userIsDm || !x.char.character.hidden)"
            :key="charInfo.character.id"
            :class="[
                'group flex h-fit flex-wrap items-center gap-4 rounded-xl border-2 p-2 transition-colors',
                {
                    'cursor-pointer hover:border-take-yellow':
                        (combatIsOpen || combatIsStarted) &&
                        isEditableForUser(charInfo),
                },
                props.listToDisplay != 'Staging' && combatIsStarted
                    ? index == combat?.initiativeIndex
                        ? 'border-take-cream'
                        : 'border-take-purple-light'
                    : '',
            ]"
            @click="
                () =>
                    isEditableForUser(charInfo)
                        ? emit('OnClickCharacter', charInfo.character)
                        : null
            ">
            <!-- Initiative value when combat initiative has been rolled -->
            <section
                v-if="
                    !combatIsOpen && isInitiativeCharacter(charInfo.character)
                "
                class="flex gap-2">
                <div
                    v-for="(value, index) in charInfo.character.initiative
                        .value"
                    :key="index"
                    :class="[
                        'flex items-center rounded-lg p-1',
                        {
                            'bg-take-navy-light':
                                index == 0 &&
                                charInfo.user.userId !=
                                    userStore.state.user?.userId,
                            'bg-take-yellow-dark text-take-purple-very-dark':
                                index == 0 &&
                                charInfo.user.userId ==
                                    userStore.state.user?.userId,
                            'bg-take-navy-medium text-xs': index != 0,
                        },
                    ]">
                    {{ value.total }}
                </div>
            </section>

            <section class="flex flex-1 flex-col ">
                <!-- Character Name -->
                <span>
                    <FontAwesomeIcon
                        v-if="
                            userIsDm &&
                            charInfo.character.playerId ==
                                userStore.state.user?.userId
                        "
                        :icon="charInfo.character.hidden ? 'eye-slash' : 'eye'"
                        size="sm" />
                    {{ charInfo.character.name }}
                </span>
                <!-- Username -->
                <span
                    :class="[
                        'text-xs',
                        {
                            'cursor-pointer':
                                isEditableForUser(charInfo) &&
                                (combatIsOpen ||
                                    combatIsStarted ||
                                    props.listToDisplay == 'Staging'),
                        },
                    ]">
                    <FontAwesomeIcon
                        class="text-take-yellow"
                        :icon="combatStore.getIconForUser(charInfo)" />
                    {{ charInfo.user?.username }}
                </span>
            </section>

            <section
                v-if="combatIsOpen && isStagedCharacter(charInfo.character)"
                class="flex items-center justify-center gap-1 rounded bg-take-purple p-1 text-sm">
                <FontAwesomeIcon icon="shoe-prints" />
                {{ charInfo.character.initiative.roll }}
            </section>

            <div
                v-if="isInitiativeCharacter(charInfo.character)"
                class="flex flex-wrap gap-2 text-sm">
                <CharacterConditionDisplay
                    v-for="(
                        condition, index
                    ) in charInfo.character.conditions.toSorted((a, b) =>
                        a.name > b.name ? 1 : -1
                    )"
                    :key="index"
                    :conditionName="condition.name" />
            </div>

            <CharacterArmourClassDisplay
                :armourClass="charInfo.character.armourClass"
                :armourClassDisplayMethod="
                    getArmourClassDisplayMethod(charInfo)
                "
                class="bg-take-purple p-1 rounded text-sm" />

            <CharacterHealthDisplay
                :health="charInfo.character.health"
                :displayMethod="getHealthDisplayMethod(charInfo)"
                class="justify w-fit rounded p-1 text-sm" />
        </li>

        <!-- When there are no characters displayed on the user, show a message -->
        <li
            v-if="
                combatIsStarted &&
                characterList
                    .map((char, index) => ({ char, index }))
                    .filter((x) => userIsDm || !x.char.character.hidden)
                    .length == 0
            ">
            <label
                v-if="characterList.find((x) => x.character.hidden)"
                class="flex justify-center text-take-grey">
                The DM has one or more hidden characters that they are waiting
                to reveal.
            </label>
            <label class="flex justify-center text-take-grey" v-else>
                There are no characters left in this combat!
            </label>
        </li>
        <li
            v-if="combatIsOpen"
            key="footer"
            :class="[
                'cursor-pointer rounded-xl border-2 border-dashed border-take-navy-light p-2 text-center transition-colors hover:border-take-yellow',
            ]"
            @click="emit('CombatOpenedStageCharacters')">
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
        ArmourClassDisplayOptionsEnum,
        type StagedCharacter,
    } from "base/utils/types/models";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const userStore = useUserStore();
    const campaignStore = useCampaignStore();
    const combatStore = useCombatStore();
    const combat = computed(() => {
        return combatStore.state.combat;
    });
    const {
        combatIsOpen,
        combatIsStarted,
        orderedStagedCharacterListWithPlayerInfo,
        initiativeListWithPlayerInfo,
        userIsDm,
    } = storeToRefs(combatStore);
    const isEditableForUser = combatStore.isEditableForUser;

    const props = withDefaults(
        defineProps<{
            listToDisplay?: "Staging" | "Initiative" | undefined;
        }>(),
        {
            listToDisplay: undefined,
        }
    );

    const emit = defineEmits<{
        (e: "CombatOpenedStageCharacters"): void;
        (
            e: "OnClickCharacter",
            character: InitiativeCharacter | StagedCharacter
        ): void;
    }>();

    const characterList: ComputedRef<
        (StagedPlayerDto | InitiativePlayerDto)[]
    > = computed(() => {
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
        character: StagedPlayerDto | InitiativePlayerDto
    ): HealthDisplayOptionValues {
        if (userIsDm.value) {
            return HealthDisplayOptionsEnum["RealValue"];
        }

        if (character.user.isDungeonMaster) {
            return campaignStore.state.campaign!.campaignSettings
                .combatHealthDisplaySettings.dmCharacterDisplayMethod!;
        }

        return campaignStore.state.campaign!.campaignSettings
            .combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod!;
    }

    function getArmourClassDisplayMethod(
        character: StagedPlayerDto | InitiativePlayerDto
    ): ArmourClassDisplayOptionValues {
        if (userIsDm.value) {
            return ArmourClassDisplayOptionsEnum.RealValue;
        }

        if (character.user.isDungeonMaster) {
            return campaignStore.state.campaign!.campaignSettings
                .combatArmourClassDisplaySettings.dmCharacterDisplayMethod!;
        }

        return campaignStore.state.campaign!.campaignSettings
            .combatArmourClassDisplaySettings
            .otherPlayerCharacterDisplayMethod!;
    }

    function isInitiativeCharacter(
        character: InitiativeCharacter | StagedCharacter
    ): character is InitiativeCharacter {
        return (
            (character as InitiativeCharacter).initiative.value !== undefined
        );
    }

    function isStagedCharacter(
        character: InitiativeCharacter | StagedCharacter
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
