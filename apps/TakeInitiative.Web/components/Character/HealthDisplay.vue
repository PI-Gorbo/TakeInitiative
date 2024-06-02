<template>
    <div
        v-if="props.health?.hasHealth"
        class="flex select-none items-center gap-2"
    >
        <FontAwesomeIcon icon="droplet" />
        {{ props.health.currentHealth }}
        {{ props.health.maxHealth ? `/ ${props.health.maxHealth}` : "" }}
    </div>
</template>
<script setup lang="ts">
import type { CharacterHealth, DisplayOptions } from "~/utils/types/models";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
const props = withDefaults(
    defineProps<{
        health: CharacterHealth | null | undefined;
        displayMethod?: DisplayOptions;
    }>(),
    {
        displayMethod: "RealValue",
    },
);
</script>
