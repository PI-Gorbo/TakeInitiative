<template>
    <div class="flex flex-col gap-2">
        <section
            v-if="hasAnyValidStages"
            :key="index"
            v-for="(stage, index) in props.stages.filter(
                (x) => (x.npcs?.length ?? 0) > 0,
            ) ?? []"
            class="text-white"
        >
            <div
                @click="(e) => onClickStage(e, stage.id)"
                :class="[
                    'flex w-full cursor-pointer flex-col rounded-xl border-2 border-take-navy-light p-2 transition-colors',
                    stagesToHighlight.includes(stage.id)
                        ? 'border-take-yellow'
                        : 'hover:border-take-grey',
                ]"
            >
                <div class="mb-2 flex w-full flex-row gap-2">
                    <div class="flex-1 text-take-yellow">
                        {{ stage.name }}
                    </div>
                </div>
                <section class="flex flex-1 flex-col gap-2 pb-4">
                    <div
                        class="flex flex-1 flex-col gap-2 overflow-y-auto"
                        tag="section"
                        name="fade"
                    >
                        <section
                            v-for="npc in stage.npcs"
                            :key="npc.id"
                            :class="[
                                'min-h-fit min-w-fit cursor-pointer rounded-xl border border-take-navy-light transition-colors ',
                                charactersToHighlight.includes(npc.id!)
                                    ? 'border-take-yellow'
                                    : 'hover:border-take-grey',
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
                                    <div
                                        class="flex select-none items-center gap-2"
                                    >
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
                <FormButton
                    label="Stage Characters"
                    loadingDisplay="Staging..."
                    :click="onSubmit"
                />
            </div>
        </Transition>
        <div v-if="!hasAnyValidStages" class="text-white">
            Looks like all the stages for this combat are empty, or there are no
            stages left!
        </div>
    </div>
</template>

<script setup lang="ts">
import type { PlannedCombatStage } from "~/utils/types/models";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type {
    PostStagePlannedCharactersRequest,
    StagePlannedCharacterDto,
} from "~/utils/api/combat/postStagePlannedCharactersRequest";

const props = defineProps<{
    stages: PlannedCombatStage[];
    submit: (
        plannedCharactersToStage: Record<string, StagePlannedCharacterDto[]>,
    ) => Promise<any>;
}>();

const state = reactive<{
    selectedStages: Record<string, StagePlannedCharacterDto[] | null>;
}>({
    selectedStages: {},
});

onMounted(() => {
    for (const val of props.stages) {
        state.selectedStages[val.id] = null;
    }
});

const stagesToHighlight: ComputedRef<string[]> = computed(() =>
    Object.entries(state.selectedStages)
        .filter(
            ([id, dto]) =>
                dto != null &&
                dto.length ==
                    props.stages.find((x) => x.id == id)?.npcs?.length,
        )
        .map(([id, dto]) => id),
);

const charactersToHighlight: ComputedRef<string[]> = computed(() =>
    Object.entries(state.selectedStages)
        .filter(([id, dto]) => dto != null)
        .map(([id, dto]) => dto)
        .flatMap((dto) => dto)
        .map((x) => x?.characterId!),
);

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
        state.selectedStages[stageId] = null;
    }
}

function onClickCharacter(
    event: MouseEvent,
    stageId: string,
    characterId: string,
) {
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
        state.selectedStages[stageId]!.find(
            (x) => x.characterId == characterId,
        ) != null;
    if (!characterExists) {
        state.selectedStages[stageId]!.push({
            characterId: characterId,
            quantity: props.stages
                .find((x) => x.id == stageId)
                ?.npcs?.find((x) => x.id == characterId)?.quantity!,
        });
    } else {
        state.selectedStages[stageId] = state.selectedStages[stageId]!.filter(
            (x) => x.characterId != characterId,
        );

        if (state.selectedStages[stageId]!.length == 0) {
            state.selectedStages[stageId] = null;
        }
    }
}

async function onSubmit() {
    const outputRecord: Record<string, StagePlannedCharacterDto[]> = {};
    for (const key in state.selectedStages) {
        if (state.selectedStages[key] != null) {
            outputRecord[key] = state.selectedStages[key]!;
        }
    }

    return props.submit(outputRecord);
}

const canSubmit = computed(
    () =>
        Object.values(state.selectedStages).filter((val) => val != null)
            .length > 0,
);

const hasAnyValidStages = computed(
    () =>
        props.stages
            .map((x) => x.npcs?.length)
            .filter((length) => (length ?? 0) > 0).length > 0,
);
</script>
