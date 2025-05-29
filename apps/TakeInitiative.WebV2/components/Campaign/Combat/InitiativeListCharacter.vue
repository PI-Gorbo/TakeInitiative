<template>
    <Sheet v-model:open="open">
        <SheetTrigger asChild>
            <Button
                variant="outline"
                class="group flex p-2 gap-2 w-full h-fit justify-start text-start disabled:opacity-100"
                :class="{
                    [`${styles.interactable}`]: userIsDmOrCharacterOwner(),
                    'shadow-gold border-gold hover:shadow-gold hover:border-gold':
                        index ===
                        combatStore.combatQuery.data?.combat.initiativeIndex,
                }"
                :disabled="!userIsDmOrCharacterOwner()">
                <section
                    v-if="
                        !combatStore.combatIsOpen &&
                        isInitiativeCharacter(characterDto.character)
                    "
                    class="flex gap-2">
                    <div
                        v-for="(value, index) in characterDto.character
                            .initiative.value"
                        :key="index"
                        :class="[
                            'flex items-center rounded-lg p-1',
                            {
                                'bg-secondary text-secondary-foreground':
                                    index == 0 &&
                                    characterDto.user.userId !=
                                        userStore.state.user?.userId,
                                'bg-gold text-gold-foreground':
                                    index == 0 &&
                                    characterDto.user.userId ==
                                        userStore.state.user?.userId,
                                'bg-take-navy-medium text-xs': index != 0,
                            },
                        ]">
                        {{ value.total }}
                    </div>
                </section>
                <section
                    class="flex flex-col"
                    :class="{
                        'text-muted-foreground': characterDto.character.hidden,
                    }">
                    <!-- Character Name -->
                    <span>
                        <FontAwesomeIcon
                            v-if="
                                combatStore.userIsDm &&
                                characterDto.character.playerId ==
                                    userStore.state.user?.userId
                            "
                            :icon="
                                characterDto.character.hidden
                                    ? faEyeSlash
                                    : faEye
                            "
                            size="sm" />
                        {{ characterDto.character.name }}
                    </span>
                    <!-- Username -->
                    <span
                        :class="[
                            'text-xs',
                            {
                                'cursor-pointer':
                                    combatStore.isEditableForUser(
                                        characterDto
                                    ) &&
                                    (combatStore.combatIsOpen ||
                                        combatStore.combatIsStarted),
                            },
                        ]">
                        <FontAwesomeIcon
                            class="text-gold"
                            :icon="combatStore.getIconForUser(characterDto)" />
                        {{ characterDto.user?.username }}
                    </span>
                </section>
                <section
                    class="flex-1 flex justify-end"
                    :class="{
                        'text-muted-foreground': characterDto.character.hidden,
                    }">
                    <CampaignCombatCharacterStatsDisplay
                        :health="characterDto.character.health"
                        :armourClass="characterDto.character.armourClass"
                        :initiative="
                            isStagedCharacter(characterDto.character)
                                ? characterDto.character.initiative.roll
                                : undefined
                        " />
                </section>
            </Button>
        </SheetTrigger>
        <SheetContent>
            <CampaignCombatStageOrModifyCharacterForm
                v-if="isStagedCharacter(characterDto.character)"
                :combatId="combatId"
                :character="characterDto.character"
                @submitted="() => (open = false)" />
            <CampaignCombatModifyInitiativeCharacterForm
                v-else
                :character="characterDto.character"
                :combatId="combatId"
                @submitted="() => (open = false)" />
        </SheetContent>
    </Sheet>
</template>
<script setup lang="ts">
    import { faEye, faEyeSlash } from "@fortawesome/free-solid-svg-icons";
    import { isInitiativeCharacter, isStagedCharacter } from "./combatHelpers";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const combatStore = useCombatStore();
    const userStore = useUserStore();
    const open = ref(false);

    const {
        combatId,
        character: characterDto,
        index,
    } = defineProps<{
        combatId: string;
        character: StagedPlayerDto | InitiativePlayerDto;
        index: number;
    }>();

    function userIsDmOrCharacterOwner() {
        return (
            combatStore.userIsDm ||
            characterDto.user.userId === userStore.state.user?.userId
        );
    }
</script>
