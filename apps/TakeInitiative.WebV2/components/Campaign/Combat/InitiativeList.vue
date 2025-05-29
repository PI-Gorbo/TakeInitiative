<template>
    <TransitionGroup
        class="flex select-none flex-col gap-2"
        tag="section"
        name="shuffleList">
        <template v-if="characterList.length">
            <div
                v-for="(characterDto, index) in characterList"
                :key="characterDto.character.id"
                >
                <CampaignCombatInitiativeListCharacter
                    :character="characterDto"
                    :index="index"
                    :combatId="props.combatId" />
            </div>
        </template>
        <template v-else>
            <span
                id="no-characters-in-combat"
                class="text-muted-foreground"
                >There are no characters in this combat.</span
            >
        </template>

        <Sheet
            v-model:open="addStagedCharacterSheet"
            v-if="combatStore.combatIsOpen">
            <SheetTrigger asChild>
                <Button
                    variant="outline"
                    class="interactable border-dashed">
                    <FontAwesomeIcon :icon="faPlusCircle" />
                    Add Characters
                </Button>
            </SheetTrigger>
            <SheetContent>
                <SheetHeader>
                    <SheetTitle> Add Characters</SheetTitle>
                </SheetHeader>
                <CampaignCombatStageCharactersForm
                    @submitted="() => (addStagedCharacterSheet = false)"
                    :campaignId="props.campaignId"
                    :combatId="props.combatId"
                    :userIsDm="combatStore.userIsDm"
                    :plannedStages="
                        combatStore.combatQuery.data?.combat.plannedStages ?? []
                    " />
            </SheetContent>
        </Sheet>
    </TransitionGroup>
</template>
<script setup lang="ts">
    import { faPlusCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import type { CharacterDto } from "./combatHelpers";

    const userStore = useUserStore();
    const combatStore = useCombatStore();

    const props = defineProps<{
        campaignId: string;
        combatId: string;
    }>();

    const orderedInitiativeList = computed(() =>
        combatStore.isLoading
            ? []
            : (combatStore.combatQuery.data?.combat?.initiativeList.map(
                  (x) =>
                      ({
                          user: combatStore.getMemberDetailsFor(x.playerId)!,
                          character: x,
                      }) satisfies InitiativePlayerDto
              ) ?? [])
    );

    const characterList: ComputedRef<CharacterDto[]> = computed(() => {
        if (combatStore.isLoading) {
            return [];
        }

        if (combatStore.combatIsOpen) {
            return combatStore.orderedStagedCharacterListWithPlayerInfo;
        }

        return orderedInitiativeList.value ?? [];
    });

    // function getHealthDisplayMethod(
    //     character: StagedPlayerDto | InitiativePlayerDto
    // ): HealthDisplayOptionValues {
    //     if (userIsDm.value) {
    //         return HealthDisplayOptionsEnum["RealValue"];
    //     }

    //     if (character.user.isDungeonMaster) {
    //         return campaignQuery.data.value!.campaign!.campaignSettings
    //             .combatHealthDisplaySettings.dmCharacterDisplayMethod!;
    //     }

    //     return campaignQuery.data.value!.campaign!.campaignSettings
    //         .combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod!;
    // }

    // function getArmourClassDisplayMethod(
    //     character: StagedPlayerDto | InitiativePlayerDto
    // ): ArmourClassDisplayOptionValues {
    //     if (userIsDm.value) {
    //         return ArmourClassDisplayOptionsEnum.RealValue;
    //     }

    //     if (character.user.isDungeonMaster) {
    //         return campaignQuery.data.value!.campaign!.campaignSettings
    //             .combatArmourClassDisplaySettings.dmCharacterDisplayMethod!;
    //     }

    //     return campaignQuery.data.value!.campaign!.campaignSettings
    //         .combatArmourClassDisplaySettings
    //         .otherPlayerCharacterDisplayMethod!;
    // }

    // Add Staged Character form
    const addStagedCharacterSheet = ref(false);
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
