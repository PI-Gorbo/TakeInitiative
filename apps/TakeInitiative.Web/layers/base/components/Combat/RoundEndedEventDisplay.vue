<template>
    <main class="rounded bg-take-purple-light p-2 shadow">
        <header>Round {{ props.roundNumber }} ended</header>
    </main>
</template>
<script setup lang="ts">
import type { RoundEndedHistoryEvent } from "base/utils/types/models";

const props = defineProps<{
    event: RoundEndedHistoryEvent;
    nameMap: Record<string, string>;
    roundNumber: number;
}>();
</script>
