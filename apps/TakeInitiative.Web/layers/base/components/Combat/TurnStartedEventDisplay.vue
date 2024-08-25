<template>
    <main class="rounded bg-take-purple-light p-2 shadow">
        <header>
            {{ props.nameMap[event.characterId] ?? "UNKNOWN" }}'s turn started
        </header>
    </main>
</template>
<script setup lang="ts">
import type { TurnStartedHistoryEvent } from "base/utils/types/models";

const props = defineProps<{
    event: TurnStartedHistoryEvent & { roundNumber: number };
    nameMap: Record<string, string>;
    roundNumber: number;
}>();
</script>
