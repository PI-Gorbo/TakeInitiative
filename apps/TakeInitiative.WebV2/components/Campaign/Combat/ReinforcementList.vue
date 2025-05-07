<template>
    <ul class="flex flex-col gap-2 w-full overflow-y-auto max-h-full ">
        <li
            v-for="{
                character,
                user,
            } in combatStore.orderedStagedCharacterListWithPlayerInfo"
            :key="character.id">
            <Button
                variant="outline"
                class="interactable w-full flex justify-between h-fit">
                <div class="flex flex-col items-start">
                    <label>{{ character.name }}</label>
                    <label class="text-muted-foreground">{{
                        user.username
                    }}</label>
                </div>
                <CampaignCombatCharacterStatsDisplay
                    :armourClass="character.armourClass"
                    :health="character.health"
                    :initiative="character.initiative.roll" />
            </Button>
            <!-- <CombatStagedCharacterDisplay
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
                @clicked="() => transitionViewToEdit(character.character)" /> -->
        </li>
    </ul>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getCombatQuery } from "~/utils/queries/combats";
    import { CombatStartedHistoryEvent } from "~/utils/types/models";

    const combatStore = useCombatStore();
</script>
