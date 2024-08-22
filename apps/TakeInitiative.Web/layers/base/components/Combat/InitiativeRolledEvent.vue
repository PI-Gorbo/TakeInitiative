<template>
    <ul class="flex flex-col gap-2">
        <li
            v-for="character in props.event.rolls"
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
</template>
<script setup lang="ts">
import type { CombatInitiativeRolledEvent } from "base/utils/types/models";

const props = defineProps<{
    event: CombatInitiativeRolledEvent;
}>();
</script>
