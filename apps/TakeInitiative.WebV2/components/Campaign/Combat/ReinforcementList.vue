<template>
    <div>
        <ul class="flex flex-col gap-2 w-full overflow-y-auto max-h-full">
            <ReinforcementListCharacter
                v-for="dto in combatStore.orderedStagedCharacterListWithPlayerInfo"
                :key="dto.character.id"
                :dto="dto"
                @addStagedCharacter="
                    () => addStagedCharacter(dto.character.id)
                " />
        </ul>
        <footer class="flex justify-end">
            <Sheet v-model:open="sheetStates.addReinforcementsSheetOpen">
                <SheetTrigger asChild>
                    <Button variant="link">
                        <FontAwesomeIcon :icon="faPlusCircle" />
                        Add
                    </Button>
                </SheetTrigger>
                <SheetContent>
                    <SheetHeader>
                        <SheetTitle> Add Reinforcements </SheetTitle>
                    </SheetHeader>
                    <CampaignCombatStageCharactersForm
                        @submitted="
                            () =>
                                (sheetStates.addReinforcementsSheetOpen = false)
                        "
                        :campaignId="props.campaignId"
                        :combatId="props.combatId"
                        :userIsDm="combatStore.userIsDm"
                        :plannedStages="
                            combatStore.combat?.plannedStages!
                        " />
                </SheetContent>
            </Sheet>
        </footer>
    </div>
</template>

<script setup lang="ts">
    import { faPencil, faPlusCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useQuery } from "@tanstack/vue-query";
    import { toast } from "vue-sonner";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import {
        getCombatQuery,
        useAddStagedCharactersToCombatMutation,
    } from "~/utils/queries/combats";
    import { CombatStartedHistoryEvent } from "~/utils/types/models";
    import ReinforcementListCharacter from "./ReinforcementListCharacter.vue";

    const props = defineProps<{
        campaignId: string
        combatId: string
    }>()

    const combatStore = useCombatStore();
    const addStagedCharacterMutation = useAddStagedCharactersToCombatMutation();
    const addStagedCharacter = async (characterId: string) => {
        addStagedCharacterMutation
            .mutateAsync({
                combatId: combatStore.combatQuery.data?.combat.id!,
                characterIds: [characterId],
            })
            .then(() => toast.success("Added successfully!"));
    };

    
    // state
    const sheetStates = reactive({
        addReinforcementsSheetOpen: false,
    });
</script>
