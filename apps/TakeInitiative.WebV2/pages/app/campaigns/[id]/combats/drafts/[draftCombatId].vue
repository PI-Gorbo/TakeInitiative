<template>
    <LoadingFallback
        :isLoading="draftCombatQuery.isLoading.value"
        class="flex flex-col gap-4">
        <Card
            v-for="stage in draftCombatQuery.data.value!.stages"
            :key="stage.id"
            class="border-2 p-2 border-primary/50">
            <CampaignCombatDraftStageDisplay
                :allStages="draftCombatQuery.data.value!.stages"
                :stage="stage"
                :updateStage="(req) => updateStage.mutateAsync({ stage, req })"
                :deleteStage="() => deleteStage.mutateAsync(stage)"
                :createNpc="
                    (request) =>
                        addNpc.mutateAsync({
                            stage,
                            nonPlayerCharacter: request,
                        })
                "
                :updateNpc="
                    (request) => updateNpc.mutateAsync({ stage, npc: request })
                "
                :deleteNpc="
                    (request) => deleteNpc.mutateAsync({ stage, npc: request })
                " />
        </Card>
        <Button
            variant="outline"
            class="interactable border-dashed w-full text-muted"
            @click="
                addStage.mutate({
                    name: `Stage ${draftCombatQuery.data.value!.stages.length + 1}`,
                })
            ">
            <FontAwesomeIcon :icon="faPlusCircle" />
            {{
                addStage.isIdle.value || addStage.isSuccess.value
                    ? "Add Stage"
                    : "Adding Stage..."
            }}
        </Button>
    </LoadingFallback>
</template>
<script setup lang="ts">
    import { faPlusCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useMutation, useQuery, useQueryClient } from "@tanstack/vue-query";
    import type { CreatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
    import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
    import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
    import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
    import type { UpdatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/updatePlannedCombatStageRequest";
    import { combatQueries } from "~/utils/queries/combats";
    import {
        stagedCharacterValidator,
        type PlannedCombatStage,
    } from "~/utils/types/models";
    const queryClient = useQueryClient();
    const api = useApi();
    const route = useRoute("app-campaigns-id-combats-drafts-draftCombatId");
    definePageMeta({
        layout: "campaign-combats",
        requiresAuth: true,
    });

    const draftCombatQuery = useQuery(
        combatQueries.getDraftCombat.query(
            () => route.params.id,
            () => route.params.draftCombatId
        )
    );

    const addStage = useMutation({
        mutationFn: async (
            req: Omit<CreatePlannedCombatStageRequest, "combatId">
        ) =>
            await api.draftCombat.stage.create({
                ...req,
                combatId: route.params.draftCombatId,
            }),

        onSuccess(resp) {
            queryClient.setQueryData(
                combatQueries.getDraftCombat.key(
                    route.params.id,
                    route.params.draftCombatId
                ),
                resp
            );
        },
    });

    const addNpc = useMutation({
        mutationFn: async (req: {
            stage: PlannedCombatStage;
            nonPlayerCharacter: Omit<
                CreatePlannedCombatNpcRequest,
                "combatId" | "stageId"
            >;
        }) =>
            await api.draftCombat.stage.npc.create({
                combatId: route.params.draftCombatId,
                stageId: req.stage.id,
                ...req.nonPlayerCharacter,
            }),
        onSuccess(resp) {
            queryClient.setQueryData(
                combatQueries.getDraftCombat.key(
                    route.params.id,
                    route.params.draftCombatId
                ),
                resp
            );
        },
    });

    const updateNpc = useMutation({
        mutationFn: async (req: {
            stage: PlannedCombatStage;
            npc: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">;
        }) =>
            await api.draftCombat.stage.npc.update({
                combatId: route.params.draftCombatId,
                stageId: req.stage.id,
                ...req.npc,
            }),
        onSuccess(resp) {
            queryClient.setQueryData(
                combatQueries.getDraftCombat.key(
                    route.params.id,
                    route.params.draftCombatId
                ),
                resp
            );
        },
    });

    const deleteNpc = useMutation({
        mutationFn: async (req: {
            stage: PlannedCombatStage;
            npc: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">;
        }) =>
            await api.draftCombat.stage.npc.delete({
                combatId: route.params.draftCombatId,
                stageId: req.stage.id,
                ...req.npc,
            }),
        onSuccess(resp) {
            queryClient.setQueryData(
                combatQueries.getDraftCombat.key(
                    route.params.id,
                    route.params.draftCombatId
                ),
                resp
            );
        },
    });

    const deleteStage = useMutation({
        mutationFn: async (stage: PlannedCombatStage) =>
            await api.draftCombat.stage.delete({
                combatId: route.params.draftCombatId,
                stageId: stage.id,
            }),
        onSuccess(resp) {
            queryClient.setQueryData(
                combatQueries.getDraftCombat.key(
                    route.params.id,
                    route.params.draftCombatId
                ),
                resp
            );
        },
    });

    const updateStage = useMutation({
        mutationFn: async (req: {
            stage: PlannedCombatStage;
            req: Omit<UpdatePlannedCombatStageRequest, "combatId" | "stageId">;
        }) =>
            await api.draftCombat.stage.update({
                combatId: route.params.draftCombatId,
                stageId: req.stage.id,
                ...req.stage,
            }),
    });
</script>
