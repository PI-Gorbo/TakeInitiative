<template>
    <header
        v-if="currentCombatInfo"
        :class="[
            'cursor-pointer select-none rounded-lg px-4 py-3 text-center text-xl text-take-navy',
            currentCombatInfo.state == CombatState.Open
                ? 'bg-take-teal'
                : 'bg-take-red',
            isMobile ? 'flex-1' : 'w-full',
        ]"
        @click="
            async () => await navigateTo(`/combat/${currentCombatInfo?.id}`)
        "
    >
        <div v-if="currentCombatInfo.state == CombatState.Open">
            {{ openCombatText }}
        </div>
        <div v-else>
            {{ combatStartedText }}
        </div>
    </header>
</template>

<script setup lang="ts">
import { CombatState } from "base/utils/types/models";

const { isMobile } = useDevice();
const campaignStore = useCampaignStore();
const { state: campaignStoreState } = storeToRefs(campaignStore);
const currentCombatInfo = computed(() => {
    return campaignStoreState.value.currentCombatInfo;
});

const openCombatText = computed(() => {
    const combatDto = campaignStore.state.currentCombatInfo;
    const combatOpedByName = campaignStore.memberDtos.find(
        (x) => x.userId == combatDto?.dungeonMaster,
    )?.username;

    return `The combat '${combatDto?.combatName}' has been opened. Click to join.`;
});
const combatStartedText = computed(() => {
    const combatDto = campaignStore.state.currentCombatInfo;
    const combatOpedByName = campaignStore.memberDtos.find(
        (x) => x.userId == combatDto?.dungeonMaster,
    )?.username;

    return `The combat '${combatDto?.combatName}' has started! Click to join.`;
});
</script>
