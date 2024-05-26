<template>
    <Tabs
        :showTabs="{
            Planned: () => userIsDm && anyPlannedCharacters,
            Characters: () =>
                (campaignStore.state.userCampaignMember?.characters?.length ??
                    0) > 0,
        }"
        :renameTabs="{
            Custom: 'Add Custom',
            Planned: 'Add Planned',
        }"
        class="py-2"
        backgroundColour="take-navy-medium"
    >
        <template #Characters>
            <CombatCampaignMemberCharactersSection
                class="py-2"
                :onStage="
                    (ids) =>
                        combatStore.stagePlayerCharacters(ids)
                        .then(props.characterAdded)
                "
            />
        </template>
        <template #Custom>
            <CombatModifyStagedCharacterForm
                :onCreate="
                    (req) =>
                        combatStore
                            .upsertStagedCharacter(req)
                            .then(props.characterAdded)
                "
            />
        </template>
        <template #Planned>
            <CombatPlannedStagesDisplay
                :stages="combatStore.state.combat?.plannedStages!"
                :submit="
                    (req) =>
                        combatStore
                            .stagePlannedCharacters(req)
                            .then(props.characterAdded)
                "
            />
        </template>
    </Tabs>
</template>
<script setup lang="ts">
const campaignStore = useCampaignStore();
const combatStore = useCombatStore();
const {
    userIsDm,
    orderedStagedCharacterListWithPlayerInfo,
    anyPlannedCharacters,
} = storeToRefs(combatStore);

const props = defineProps<{
    characterAdded: () => Promise<any | unknown>;
}>();
</script>
