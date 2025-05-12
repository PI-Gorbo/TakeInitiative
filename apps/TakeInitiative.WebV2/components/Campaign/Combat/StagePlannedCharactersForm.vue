<template>
    <div class="flex flex-col gap-2 pt-2">
        <section
            v-if="hasAnyValidStages"
            :key="index"
            v-for="(stage, index) in props.plannedStages.filter(
                (x) => (x.npcs?.length ?? 0) > 0
            ) ?? []"
            class="text-white">
            <fieldset
                @click="(e) => onClickStage(e, stage.id)"
                :class="[
                    'flex w-full cursor-pointer flex-col rounded-xl border-2 border-take-navy-light p-2 transition-colors interactable',
                    stagesToHighlight.includes(stage.id)
                        ? 'border-gold'
                        : 'hover:border-primary',
                ]">
                <legend class="flex gap-2 px-2">
                    {{ stage.name }}
                </legend>
                <section class="flex flex-1 flex-col gap-2 pb-4">
                    <div
                        class="flex flex-1 flex-col gap-2 overflow-y-auto"
                        tag="section"
                        name="fade">
                        <button
                            v-for="npc in stage.npcs"
                            variant="outline"
                            :key="npc.id"
                            :class="[
                                'min-h-fit min-w-fit cursor-pointer rounded-xl border border-take-navy-light transition-colors interactable',
                                charactersToHighlight.includes(npc.id!)
                                    ? 'border-gold'
                                    : 'hover:border-primary',
                            ]"
                            @click="
                                (e) => onClickCharacter(e, stage.id, npc.id!)
                            ">
                            <div class="p-2">
                                <div
                                    class="flex cursor-pointer flex-wrap justify-between gap-2 px-2">
                                    <div
                                        class="ws-nowrap cursor-pointer select-none text-lg">
                                        {{ npc.name }} ( x {{ npc.quantity }} )
                                    </div>

                                    <CharacterHealthDisplay
                                        :health="npc.health" />

                                    <CharacterArmourClassDisplay
                                        :armourClass="npc.armourClass" />

                                    <div
                                        class="flex select-none items-center gap-2">
                                        <FontAwesomeIcon icon="shoe-prints" />
                                        <div>
                                            {{ npc.initiative.roll }}
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </button>
                    </div>
                </section>
            </fieldset>
        </section>

        <div class="flex w-full justify-end" v-if="hasAnyValidStages">
            <AsyncButton
                loadingLabel="Staging..."
                :icon="faPlusCircle"
                label="Add Characters"
                :click="onSubmit"
                :disabled="!canSubmit" />
        </div>

        <div v-if="!hasAnyValidStages" class="text-white">
            Looks like all the stages for this combat are empty, or there are no
            stages left!
        </div>
    </div>
</template>

<script setup lang="ts">
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import type { StagePlannedCharacterDto } from "~/utils/api/combat/postStagePlannedCharactersRequest";
    import type { GetCombatResponse } from "~/utils/api/combat/getCombatRequest";
    import { useAddPlannedCharacterToCombatMutation } from "~/utils/queries/combats";
import { faPlusCircle } from "@fortawesome/free-solid-svg-icons";

    const emits = defineEmits<{
        submit: [];
    }>();
    const props = defineProps<{
        combatId: string;
        plannedStages: GetCombatResponse["combat"]["plannedStages"];
    }>();

    const state = reactive<{
        selectedStages: Record<string, StagePlannedCharacterDto[] | null>;
    }>({
        selectedStages: {},
    });

    onMounted(() => {
        for (const val of props.plannedStages) {
            state.selectedStages[val.id] = null;
        }
    });

    const stagesToHighlight: ComputedRef<string[]> = computed(() =>
        Object.entries(state.selectedStages)
            .filter(
                ([id, dto]) =>
                    dto != null &&
                    dto.length ==
                        props.plannedStages.find((x) => x.id == id)?.npcs
                            ?.length
            )
            .map(([id, dto]) => id)
    );

    const charactersToHighlight: ComputedRef<string[]> = computed(() =>
        Object.entries(state.selectedStages)
            .filter(([id, dto]) => dto != null)
            .map(([id, dto]) => dto)
            .flatMap((dto) => dto)
            .map((x) => x?.characterId!)
    );

    function onClickStage(event: MouseEvent, stageId: string) {
        event.stopPropagation(); // Stops bubbling
        if (state.selectedStages[stageId] == null) {
            state.selectedStages[stageId] = props.plannedStages
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
        characterId: string
    ) {
        event.stopPropagation();
        if (state.selectedStages[stageId] == null) {
            state.selectedStages[stageId] = [
                {
                    characterId: characterId,
                    quantity: props.plannedStages
                        .find((x) => x.id == stageId)
                        ?.npcs?.find((x) => x.id == characterId)?.quantity!,
                },
            ];
            return;
        }

        const characterExists =
            state.selectedStages[stageId]!.find(
                (x) => x.characterId == characterId
            ) != null;
        if (!characterExists) {
            state.selectedStages[stageId]!.push({
                characterId: characterId,
                quantity: props.plannedStages
                    .find((x) => x.id == stageId)
                    ?.npcs?.find((x) => x.id == characterId)?.quantity!,
            });
        } else {
            state.selectedStages[stageId] = state.selectedStages[
                stageId
            ]!.filter((x) => x.characterId != characterId);

            if (state.selectedStages[stageId]!.length == 0) {
                state.selectedStages[stageId] = null;
            }
        }
    }

    const stagedPlannedCharaterMutation =
        useAddPlannedCharacterToCombatMutation();
    async function onSubmit() {
        const outputRecord: Record<string, StagePlannedCharacterDto[]> = {};
        for (const key in state.selectedStages) {
            if (state.selectedStages[key] != null) {
                outputRecord[key] = state.selectedStages[key]!;
            }
        }

        await stagedPlannedCharaterMutation.mutateAsync({
            plannedCharactersToStage: outputRecord,
            combatId: props.combatId,
        });

        emits('submit')
    }

    const canSubmit = computed(
        () =>
            Object.values(state.selectedStages).filter((val) => val != null)
                .length > 0
    );

    const hasAnyValidStages = computed(
        () =>
            (
                props.plannedStages
                    ?.map((x) => x.npcs?.length)
                    .filter((length) => (length ?? 0) > 0) ?? 0
            ).length > 0
    );
</script>
