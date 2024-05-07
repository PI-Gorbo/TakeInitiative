<template>
    <TransitionGroup
        class="flex h-full flex-1 select-none flex-col gap-2 overflow-y-auto rounded-lg"
        tag="ul"
        name="shuffleList"
    >
        <!-- INITIATIVE LIST -->

        <li
            v-for="(charInfo, index) in characterList"
            :key="charInfo.character.id"
            :class="[
                'grid grid-cols-2 rounded-xl border-2 border-take-navy-light p-2 transition-colors',
                {
                    'cursor-pointer hover:border-take-yellow':
                        combatIsOpen && isEditableForUser(charInfo),
                    'border-take-yellow':
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
            <header class="flex items-center gap-2 text-white">
                <!-- Initiative -->
                <div
                    v-if="!combatIsOpen"
                    :class="[
                        {
                            'cursor-pointer':
                                isEditableForUser(charInfo) &&
                                (combatIsOpen ||
                                    props.listToDisplay == 'Staging'),
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
                <!-- Username -->
                <label
                    :class="[
                        {
                            'cursor-pointer':
                                isEditableForUser(charInfo) &&
                                (combatIsOpen ||
                                    props.listToDisplay == 'Staging'),
                        },
                    ]"
                >
                    <FontAwesomeIcon
                        class="text-take-yellow"
                        :icon="combatStore.getIconForUser(charInfo)"
                    />
                    {{ charInfo.user?.username }}:</label
                >
                <!-- Character Name -->
                <label
                    :class="[
                        {
                            'cursor-pointer':
                                isEditableForUser(charInfo) &&
                                (combatIsOpen ||
                                    props.listToDisplay == 'Staging'),
                        },
                    ]"
                    >{{ charInfo.character.name }}
                    {{
                        charInfo.character.copyNumber != null
                            ? `(${charInfo.character.copyNumber})`
                            : ""
                    }}</label
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
import type { CampaignMemberDto } from "~/utils/api/campaign/getCampaignRequest";
import { type CombatCharacter, CombatState } from "~/utils/types/models";
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
    combatIsFinished,
    orderedStagedCharacterListWithPlayerInfo,
    initiativeListWithPlayerInfo,
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
    (e: "OnClickCharacter", character: CombatCharacter): void;
}>();

const characterList = computed(() => {
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
