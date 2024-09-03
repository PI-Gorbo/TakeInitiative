<template>
    <div
        class="flex items-center gap-1 rounded bg-take-purple-light p-2 shadow"
    >
        {{ props.nameMap[props.event.characterId] }} lost the
        <CharacterConditionDisplay
            :conditionName="props.event.name"
            class="shadow"
        />
        condition
    </div>
</template>
<script setup lang="ts">
import type { CharacterConditionRemovedHistoryEvent } from "base/utils/types/models";
import type { HistoryEventComponentProps } from "./HistoryEventsDisplay.vue";

const props =
    defineProps<
        HistoryEventComponentProps<CharacterConditionRemovedHistoryEvent>
    >();
</script>
