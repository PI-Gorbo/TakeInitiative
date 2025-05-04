<template>
    <ul>
        <li
            v-for="character in orderedStagedCharacterListWithPlayerInfo"
            :key="character.character.id"
            class="flex flex-row items-center gap-2">
            <CombatStagedCharacterDisplay
                :charInfo="character"
                :class="[
                    'flex-1 select-none',
                    userIsDm && 'cursor-pointer',
                    clickedList.includes(character.character.id) &&
                        'border-take-yellow',
                ]"
                @click="() => onCharacterClicked(character)" />
            <FormButton
                v-if="combatStore.isEditableForUser(character)"
                icon="wrench"
                size="sm"
                buttonColour="take-navy-light"
                class="h-min"
                @clicked="() => transitionViewToEdit(character.character)" />
        </ul>
    </ul>
</template>

<script setup lang="ts">

    


    const orderedStagedCharacterListWithPlayerInfo: ComputedRef<
        StagedPlayerDto[]
    > = computed(() => {
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
            a: StagedPlayerDto,
            b: StagedPlayerDto,
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

        return (
            state.combat?.stagedList
                .map(
                    (x) =>
                        ({
                            user: campaignStore.getMemberDetailsFor(
                                x.playerId,
                            )!,
                            character: x,
                        }) satisfies StagedPlayerDto,
                )
                .sort(openCombatCharacterSortFunc) ?? []
        );
    });

</script>
