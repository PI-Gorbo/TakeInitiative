<template>
    <div class="flex flex-wrap gap-1">
        <CampaignCharacterArmourClassDisplay
            v-if="props.armourClass != 'NO VALUE'"
            :armourClass="props.armourClass"
            :armourClassDisplayMethod="armourClassDisplayMethod" />
        <CampaignCharacterHealthDisplay
            v-if="props.health"
            :displayMethod="props.healthDisplayMethod"
            :health="props.health"
            :unstyled="props.healthDisplayMethod === undefined"
             />
        <CampaignCharacterInitiativeDisplay
            v-if="props.initiative"
            :initiative="props.initiative" />
    </div>
</template>
<script setup lang="ts">
    import type {
        ArmourClassDisplayOptionValues,
        CharacterHealth,
        HealthDisplayOptionValues,
        UnevaluatedCharacterHealth,
    } from "~/utils/types/models";

    const props = withDefaults(
        defineProps<{
            health?: UnevaluatedCharacterHealth | CharacterHealth;
            armourClass?: number | null | undefined | "NO VALUE";
            initiative?: string;
            healthDisplayMethod?: HealthDisplayOptionValues
            armourClassDisplayMethod?: ArmourClassDisplayOptionValues;
        }>(),
        {
            armourClass: "NO VALUE",
        }
    );
</script>
