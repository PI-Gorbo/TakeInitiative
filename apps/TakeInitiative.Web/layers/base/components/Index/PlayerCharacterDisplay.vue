<template>
    <div
        class="flex flex-1 cursor-pointer flex-wrap justify-between gap-2 rounded-md border border-take-purple bg-take-purple px-2 transition-colors hover:border-take-yellow">
        <div
            class="flex items-center justify-center p-1 font-NovaCut md:text-lg">
            {{ props.character.name }}
        </div>

        <div class="flex items-center p-2">
            <CharacterHealthDisplay :health="props.character?.health" />
        </div>

        <div
            v-if="props.character.armourClass"
            class="flex select-none items-center gap-2">
            <FontAwesomeIcon icon="shield-halved" />
            <div class="ws-nowrap min-w-fit">
                {{ props.character.armourClass }}
            </div>
        </div>
        <div class="flex select-none items-center gap-2">
            <FontAwesomeIcon icon="shoe-prints" />
            <div>{{ props.character.initiative.roll }}</div>
        </div>
    </div>
</template>
<script setup lang="ts">
    import type { PlayerCharacter } from "base/utils/types/models";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    const props = defineProps<{
        character: PlayerCharacter;
    }>();
</script>
