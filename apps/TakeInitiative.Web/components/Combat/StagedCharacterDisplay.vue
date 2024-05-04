<template>
    <li
        :key="props.charInfo.character.id"
        :class="[
            'grid grid-cols-2 rounded-xl border-2 border-take-navy-light p-2 text-white transition-colors',
        ]"
        @click="() => emit('clicked')"
    >
        <!-- Username -->
        <span class="truncate">
            <FontAwesomeIcon
                class="text-take-yellow"
                :icon="combatStore.getIconForUser(charInfo)"
            />
            {{ charInfo.user?.username }}
        </span>
        <!-- Character Name -->
        <span class="truncate">
            {{ charInfo.character.name }}
            {{
                // Display copy number if the charInfo has one.
                charInfo.character.copyNumber != null
                    ? `(${charInfo.character.copyNumber})`
                    : ""
            }}
        </span>
    </li>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
const combatStore = useCombatStore();
const emit = defineEmits<{
    (e: "clicked"): void;
}>();
const props = withDefaults(
    defineProps<{
        charInfo: CombatPlayerDto;
    }>(),
    {},
);
</script>
