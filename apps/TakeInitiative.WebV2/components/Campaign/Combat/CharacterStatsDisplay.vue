<template>
    <div class="flex flex-wrap gap-1">
        <CampaignCharacterHealthDisplay :health="props.health" unstyled />
        <CampaignCharacterArmourClassDisplay
            :armourClass="props.armourClass" />
        <CampaignCharacterInitiativeDisplay
            :initiative="props.initiative" />
    </div>
</template>
<script setup lang="ts">
    import type {
        CharacterHealth,
        UnevaluatedCharacterHealth,
    } from "~/utils/types/models";

    const props = defineProps<{
        health: UnevaluatedCharacterHealth | CharacterHealth;
        armourClass: number | null | undefined;
        initiative: string
    }>();
</script>
