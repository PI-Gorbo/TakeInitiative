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
                        userIsDm ||
                        user.userId === userStore.state.user?.userId,
                }">
                <section
                    v-if="!combatIsOpen && isInitiativeCharacter(character)"
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
    const props = defineProps<{
        campaignId: string;
        combatId: string;
    }>();

    const campaignQuery = useQuery(getCampaignQuery(() => props.campaignId));
    const combatQuery = useQuery(
        getCombatQuery(
            () => props.campaignId,
            () => props.combatId
        )
    );

    const isLoading = computed(
        () => campaignQuery.isLoading.value || combatQuery.isLoading.value
    );

    // Member Details
    const userIsDm = computed(() => {
        return (
            userStore.state.user?.userId ===
            combatQuery.data.value?.combat.dungeonMaster
        );
    });
    const memberDtos: ComputedRef<CampaignMemberDto[]> = computed(() => {
        if (isLoading.value) {
            return [];
        }

        const list: CampaignMemberDto[] = [
            ...campaignQuery.data.value?.campaignMembers!,
            {
                ...campaignQuery.data.value!.userCampaignMember,
                username: userStore.state.user?.username!,
            },
        ];

        return list;
    });

    const getMemberDetailsFor = (id: string): CampaignMemberDto | undefined =>
        memberDtos.value.find((x) => x.userId == id);

    const orderedInitiativeList = computed(() =>
        isLoading.value
            ? []
            : (combatQuery.data.value?.combat?.initiativeList.map(
                  (x) =>
                      ({
                          user: getMemberDetailsFor(x.playerId)!,
                          character: x,
                      }) satisfies InitiativePlayerDto
              ) ?? [])
    );

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
            b: StagedPlayerDto
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
            combatQuery.data.value?.combat?.stagedList
                .map(
                    (x) =>
                        ({
                            user: getMemberDetailsFor(x.playerId)!,
                            character: x,
                        }) satisfies StagedPlayerDto
                )
                .sort(openCombatCharacterSortFunc) ?? []
        );
    });

    const characterList: ComputedRef<
        (StagedPlayerDto | InitiativePlayerDto)[]
    > = computed(() => {
        if (isLoading.value) {
            return [];
        }

        if (combatQuery.data.value?.combat.state === CombatState.Open) {
            return orderedStagedCharacterListWithPlayerInfo.value;
        }

        return orderedInitiativeList.value ?? [];
    });

    function getHealthDisplayMethod(
        character: StagedPlayerDto | InitiativePlayerDto
    ): HealthDisplayOptionValues {
        if (userIsDm.value) {
            return HealthDisplayOptionsEnum["RealValue"];
        }

        if (character.user.isDungeonMaster) {
            return campaignQuery.data.value!.campaign!.campaignSettings
                .combatHealthDisplaySettings.dmCharacterDisplayMethod!;
        }

        return campaignQuery.data.value!.campaign!.campaignSettings
            .combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod!;
    }

    function getArmourClassDisplayMethod(
        character: StagedPlayerDto | InitiativePlayerDto
    ): ArmourClassDisplayOptionValues {
        if (userIsDm.value) {
            return ArmourClassDisplayOptionsEnum.RealValue;
        }

        if (character.user.isDungeonMaster) {
            return campaignQuery.data.value!.campaign!.campaignSettings
                .combatArmourClassDisplaySettings.dmCharacterDisplayMethod!;
        }

        return campaignQuery.data.value!.campaign!.campaignSettings
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

    const combatIsOpen = computed(
        () => combatQuery.data.value?.combat.state == CombatState.Open
    );
    const combatIsStarted = computed(
        () => combatQuery.data.value?.combat.state == CombatState.Started
    );
    const combatIsFinished = computed(
        () => combatQuery.data.value?.combat.state == CombatState.Finished
    );
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
