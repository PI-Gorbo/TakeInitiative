<template>
    <Tabs
        v-if="showPlannedTab || showCharactersTab"
        :showTabs="{
            Planned: () => showPlannedTab,
            Characters: () => showCharactersTab,
        }"
        :renameTabs="{
            Custom: 'Custom',
            Planned: 'Planned',
        }"
        class="py-2"
        backgroundColour="take-purple"
        notSelectedTabColour="take-purple-light"
        selectedTabColour="take-yellow"
    >
        <template #Characters>
            <CombatCampaignMemberCharactersSection
                class="py-2"
                :onStage="
                    (ids) =>
                        combatStore
                            .stagePlayerCharacters(ids)
                            .then(props.characterAdded)
                "
            />
        </template>
        <template #Custom>
            <CombatModifyStagedCharacterForm
                :onCreate="
                    (req) =>
                        combatStore
                            .addStagedCharacter(req)
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
    <CombatModifyStagedCharacterForm
        v-else
        :onCreate="
            (req) =>
                combatStore.addStagedCharacter(req).then(props.characterAdded)
        "
    />
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

const showPlannedTab = computed(
    () => userIsDm.value && anyPlannedCharacters.value,
);
const showCharactersTab = computed(
    () => (campaignStore.state.userCampaignMember?.characters?.length ?? 0) > 0,
);
</script>
