<template>
    <main class="rounded bg-take-purple-light p-2 shadow">
        <header>
            {{ props.nameMap[event.characterId] ?? "UNKNOWN" }}'s turn ended
        </header>
    </main>
</template>
<script setup lang="ts">
import type { TurnEndedHistoryEvent } from "base/utils/types/models";

const props = defineProps<{
    event: TurnEndedHistoryEvent & { roundNumber: number };
    nameMap: Record<string, string>;
    roundNumber: number;
}>();
</script>
