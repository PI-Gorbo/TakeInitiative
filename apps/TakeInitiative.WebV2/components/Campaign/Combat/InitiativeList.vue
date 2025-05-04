<template>
    <TransitionGroup
        class="flex select-none flex-col gap-2"
        tag="section"
        name="shuffleList">
        <a
            v-for="({ character, user }, index) in characterList"
            :key="character.id">
            <Card
                class="group flex p-2 items-center gap-2"
                :class="{
                    'shadow-solid-sm interactable':
                        combatStore.userIsDm ||
                        user.userId === userStore.state.user?.userId,
                }">
                <section
                    v-if="
                        !combatStore.combatIsOpen &&
                        isInitiativeCharacter(character)
                    "
                    class="flex gap-2">
                    <div
                        v-for="(value, index) in character.initiative.value"
                        :key="index"
                        :class="[
                            'flex items-center rounded-lg p-1',
                            {
                                'bg-secondary text-secondary-foreground':
                                    index == 0 &&
                                    user.userId != userStore.state.user?.userId,
                                'bg-gold text-gold-foreground':
                                    index == 0 &&
                                    user.userId == userStore.state.user?.userId,
                                'bg-take-navy-medium text-xs': index != 0,
                            },
                        ]">
                        {{ value.total }}
                    </div>
                </section>
                {{ character.name }}
            </Card>
        </a>
    </TransitionGroup>
</template>
<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import type { CampaignMemberDto } from "~/utils/api/campaign/getCampaignRequest";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getCombatQuery } from "~/utils/queries/combats";
    import {
        ArmourClassDisplayOptionsEnum,
        CombatState,
        HealthDisplayOptionsEnum,
        type ArmourClassDisplayOptionValues,
        type HealthDisplayOptionValues,
        type InitiativeCharacter,
        type StagedCharacter,
    } from "~/utils/types/models";

    const userStore = useUserStore();
    const combatStore = useCombatStore();

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

    const characterList: ComputedRef<
        (StagedPlayerDto | InitiativePlayerDto)[]
    > = computed(() => {
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
