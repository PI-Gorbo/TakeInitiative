<template>
    <main class="rounded bg-take-purple-light p-2 shadow">
        <header>Rolled Initiative</header>
        <ul class="flex flex-col gap-2">
            <li
                v-for="character in rolls"
                class="grid grid-cols-2 items-center gap-2 px-2"
            >
                <span>{{ character.characterName }}</span>
                <div class="flex gap-2">
                    <div
                        v-for="(value, index) in character.roll"
                        :class="[
                            'flex w-min items-center rounded-lg  p-1',
                            {
                                'text-xs': index != 0,
                                'bg-take-navy-light': index == 0,
                                'bg-take-navy': index != 0,
                            },
                        ]"
                    >
                        {{ value }}
                    </div>
                </div>
            </li>
        </ul>
    </main>
</template>
<script setup lang="ts">
import type {
    CombatInitiativeModifiedHistoryEvent,
    CombatInitiativeRolledHistoryEvent,
} from "base/utils/types/models";

const props = defineProps<{
    event:
        | CombatInitiativeRolledHistoryEvent
        | CombatInitiativeModifiedHistoryEvent;
}>();
const rolls = computed(() =>
    props.event["!"] == "CombatInitiativeRolled"
        ? props.event.rolls
        : props.event.newInitiativeList,
);
</script>
