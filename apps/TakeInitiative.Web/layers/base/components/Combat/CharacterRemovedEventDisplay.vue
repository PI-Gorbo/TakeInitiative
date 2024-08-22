<template>
    <main class="rounded bg-take-purple-light p-2 shadow">
        <header>
            {{ props.nameMap[event.characterId] ?? "UNKNOWN" }} was removed from
            combat
        </header>
    </main>
</template>
<script setup lang="ts">
import type { CharacterRemovedHistoryEvent } from "base/utils/types/models";

const props = defineProps<{
    event: CharacterRemovedHistoryEvent;
    nameMap: Record<string, string>;
    roundNumber: number;
}>();
</script>
