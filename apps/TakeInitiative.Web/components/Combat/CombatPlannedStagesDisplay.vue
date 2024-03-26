<template>
    <div class="flex flex-col gap-2">
        <section
            :key="index"
            v-for="(stage, index) in props.stages ?? []"
            class="text-white"
        >
            <div
                @click="(e) => onClickStage(e, stage.id)"
                :class="[
                    'flex w-full cursor-pointer flex-col rounded-xl border-2 border-take-navy-light p-2 transition-colors',
                    {
                        'border-take-yellow':
                            state.selectedStages[stage.id] != null &&
                            state.selectedStages[stage.id].length ==
                                props.stages.find((x) => (x.id = stage.id))?.npcs?.length,
                        'hover:border-take-grey': state.selectedStages[stage.id] == null,
                    },
                ]"
            >
                <div class="mb-2 flex w-full flex-row gap-2">
                    <div class="flex-1 text-take-yellow">
                        {{ stage.name }}
                    </div>
                </div>
                <section class="flex flex-1 flex-col gap-2 pb-4">
                    <div class="flex flex-1 flex-col gap-2" tag="section" name="fade">
                        <section
                            v-for="npc in stage.npcs"
                            :key="npc.id"
                            :class="[
                                'min-h-fit min-w-fit cursor-pointer rounded-xl border border-take-navy-light transition-colors ',
                                {
                                    'border-take-yellow':
                                        state.selectedStages[stage.id]?.find(
                                            (x) => x.characterId == npc.id
                                        ) != null,
                                    'hover:border-take-grey':
                                        state.selectedStages[stage.id]?.find(
                                            (x) => x.characterId == npc.id
                                        ) == null,
                                },
                            ]"
                            @click="
                                (e) => onClickCharacter(e, stage.id, npc.id!)
                            "
                        >
                            <div class="p-2">
                                <div
                                    class="flex cursor-pointer justify-between gap-2 px-2"
                                >
                                    <div
                                        class="ws-nowrap cursor-pointer select-none text-lg"
                                    >
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
                                    <div class="flex select-none items-center gap-2">
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
        <Transition name="fade">
            <div class="flex w-full justify-end" v-if="canSubmit">
                <FormButton label="Stage Characters" />
            </div>
        </Transition>
    </div>
</template>

<script setup lang="ts">
import type { PlannedCombatStage } from "~/utils/types/models";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { PostStagePlannedCharactersRequest } from "~/utils/api/combat/postStagePlannedCharactersRequest";

const props = defineProps<{
    stages: PlannedCombatStage[];
}>();

const state = reactive<{
    selectedStages: PostStagePlannedCharactersRequest["character"];
}>({
    selectedStages: {},
});

function onClickStage(event: MouseEvent, stageId: string) {
    event.stopPropagation(); // Stops bubbling
    if (state.selectedStages[stageId] == null) {
        state.selectedStages[stageId] = props.stages
            .find((x) => x.id == stageId)
            ?.npcs?.map((npc) => ({
                characterId: npc.id!,
                quantity: npc.quantity!,
            }))!;
    } else {
        delete state.selectedStages[stageId];
    }
}

function onClickCharacter(event: MouseEvent, stageId: string, characterId: string) {
    event.stopPropagation();
    if (state.selectedStages[stageId] == null) {
        state.selectedStages[stageId] = [
            {
                characterId: characterId,
                quantity: props.stages
                    .find((x) => x.id == stageId)
                    ?.npcs?.find((x) => x.id == characterId)?.quantity!,
            },
        ];
        return;
    }

    const characterExists =
        state.selectedStages[stageId].find((x) => x.characterId == characterId) != null;
    if (!characterExists) {
        state.selectedStages[stageId].push({
            characterId: characterId,
            quantity: props.stages
                .find((x) => x.id == stageId)
                ?.npcs?.find((x) => x.id == characterId)?.quantity!,
        });
    } else {
        state.selectedStages[stageId] = state.selectedStages[stageId].filter(
            (x) => x.characterId != characterId
        );

        if (state.selectedStages[stageId].length == 0) {
            delete state.selectedStages[stageId];
        }
    }
}

const canSubmit = computed(() => Object.keys(state.selectedStages).length > 0);
</script>
