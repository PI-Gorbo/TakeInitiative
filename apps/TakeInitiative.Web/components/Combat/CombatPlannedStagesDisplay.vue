<template>
    <section v-for="(stage, index) in props.stages ?? []" class="text-white">
        <div class="flex w-full flex-col rounded-xl border-2 border-take-navy-light p-2">
            <div class="mb-2 flex w-full flex-row gap-2">
                <div class="flex-1 text-take-yellow">
                    {{ stage.name }}
                </div>
            </div>
            <section class="flex flex-col flex-1 gap-2 pb-4">
                <div class="flex flex-1 flex-col gap-2" tag="section" name="fade">
                    <section
                        v-for="npc in stage.npcs"
                        :key="npc.id"
                        class="min-h-fit min-w-fit rounded-xl border border-take-navy-light hover:border-take-yellow cursor-pointer"
                    >
                        <div class="p-2">
                            <div class="cursor-pointer flex gap-2 justify-between px-2">
                                <div class="cursor-pointer text-lg ws-nowrap select-none">
                                    {{ npc.name }} ( x {{ npc.quantity }} )
                                </div>
                                <div v-if="npc.health">
                                    <FontAwesomeIcon icon="droplet" />
                                    {{ npc.health.currentHealth }}
                                    {{
                                        npc.health.maxHealth
                                            ? `/ ${npc.health.maxHealth}`
                                            : ""
                                    }}
                                </div>
                                <div v-if="npc.armorClass">
                                    <FontAwesomeIcon icon="shield-halved" />
                                    <div class="ws-nowrap min-w-fit">
                                        {{ npc.armorClass }}
                                    </div>
                                </div>
                                <div class="flex gap-2 items-center select-none">
                                    <FontAwesomeIcon icon="shoe-prints" />
                                    <div>
                                        {{ npc.initiative.value }}
                                    </div>
                                </div>
                            </div>
                        </div>
                    </section>
                </div>
            </section>
        </div>
    </section>
</template>

<script setup lang="ts">
import type { PlannedCombatStage } from "~/utils/types/models";

const props = defineProps<{
    stages: PlannedCombatStage[];
}>();
</script>
