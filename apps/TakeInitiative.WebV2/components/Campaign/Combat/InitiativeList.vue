<template></template>
<script setup lang="ts">
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
