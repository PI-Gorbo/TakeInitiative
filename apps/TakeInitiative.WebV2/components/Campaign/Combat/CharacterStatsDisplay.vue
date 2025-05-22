<template>
    <div class="flex flex-wrap gap-1">
        <CampaignCharacterArmourClassDisplay
            v-if="props.armourClass != 'NO VALUE'"
            :armourClass="props.armourClass" />
        <CampaignCharacterHealthDisplay
            v-if="props.health"
            :health="props.health"
            unstyled />
        <CampaignCharacterInitiativeDisplay
            v-if="props.initiative"
            :initiative="props.initiative" />
    </div>
</template>
<script setup lang="ts">
    import type {
        CharacterHealth,
        UnevaluatedCharacterHealth,
    } from "~/utils/types/models";

    const props = withDefaults(
        defineProps<{
            health?: UnevaluatedCharacterHealth | CharacterHealth;
            armourClass?: number | null | undefined | "NO VALUE";
            initiative?: string;
        }>(),
        {
            armourClass: "NO VALUE",
        }
    );
</script>
