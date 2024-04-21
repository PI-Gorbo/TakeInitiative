<template>
    <TransitionGroup
        class="flex h-full flex-1 select-none flex-col gap-2 rounded-lg border-2 border-take-navy-light p-2"
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
                        combatIsStarted && index == combat?.initiativeIndex,
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
                <!-- Initiative -->
                <div
                    v-if="!combatIsOpen"
                    :class="[
                        {
                            'cursor-pointer':
                                isEditableForUser(charInfo) && combatIsOpen,
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
                                isEditableForUser(charInfo) && combatIsOpen,
                        },
                    ]"
                >
                    <FontAwesomeIcon
                        class="text-take-yellow"
                        :icon="getIconForUser(charInfo)"
                    />
                    {{ charInfo.user?.username }}:</label
                >
                <!-- Character Name -->
                <label
                    :class="[
                        {
                            'cursor-pointer':
                                isEditableForUser(charInfo) && combatIsOpen,
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
            @click="() => showModal('Stage Characters')"
        >
            Stage Characters
        </li>
    </TransitionGroup>
</template>
<script setup lang="ts">
import type { CampaignMemberDto } from "~/utils/api/campaign/getCampaignRequest";
import { type CombatCharacter, CombatState } from "~/utils/types/models";

const campaignStore = useCampaignStore();
const combatStore = useCombatStore();
const combat = computed(() => {
    return combatStore.state.combat;
});

const props = withDefaults(
    defineProps<{
        listToDisplay?: "Staging" | "Initiative" | undefined;
    }>(),
    {},
);

const emit = defineEmits<{
    (e: "StageCharacters"): void;
    (e: "OnClickCharacter"): void;
}>();

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

    let list;
    let listType = "Staging";
    if (props.listToDisplay == undefined) {
        if (combat.value?.state == CombatState.Open) {
            list = combat.value.stagedList;
        } else {
            list = combat.value?.initiativeList;
            listType = "Initiative";
        }
    } else if (props.listToDisplay == "Staging") {
        list = combat.value?.stagedList;
    } else {
        list = combat.value?.initiativeList;
        listType = "Initiative";
    }

    const sortFunc = listType == "Staging" ? openCombatCharacterSortFunc : null;

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
