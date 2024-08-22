<template>
    <main class="rounded bg-take-purple-light p-2 shadow">
        <header>
            {{ props.nameMap[event.characterId] ?? "UNKNOWN" }}
            {{ changeInHealthWording }}
            ({{ props.event.from }} -> {{ props.event.to }})
        </header>
    </main>
</template>
<script setup lang="ts">
import type { CharacterHealthChangedHistoryEvent } from "base/utils/types/models";

const props = defineProps<{
    event: CharacterHealthChangedHistoryEvent;
    nameMap: Record<string, string>;
    roundNumber: number;
}>();

const changeInHealth = computed(() => props.event.from - props.event.to);
const changeInHealthWording = computed(() =>
    changeInHealth.value > 0
        ? `took ${changeInHealth.value} damage`
        : `healed ${changeInHealth.value}`,
);
</script>
