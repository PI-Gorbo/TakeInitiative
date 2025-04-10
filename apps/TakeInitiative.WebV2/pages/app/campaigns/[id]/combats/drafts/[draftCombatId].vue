<template>
    <LoadingFallback
        :isLoading="draftCombatQuery.isLoading.value"
        class="flex flex-col gap-4">
        <div class="flex gap-2">
            <NuxtLink
                v-if="!screenSize.isLargeScreen.value"
                :to="{
                    name: 'app-campaigns-id-combats',
                    params: { id: route.params.id },
                }">
                <Button size="icon" variant="outline">
                    <FontAwesomeIcon :icon="faArrowLeft" />
                </Button>
            </NuxtLink>
            <form>
                <FormFieldWrapper
                    label="Combat Name"
                    :error="form.errors.value.name">
                    <template #Header>
                        <AsyncSuccessIcon
                            :state="updateCombatName.state.value" />
                    </template>
                    <template #default>
                        <Input
                            v-model="combatName"
                            class="bg-background"
                            @input="submitUpdateCombatName" />
                    </template>
                </FormFieldWrapper>
            </form>
            <div class="flex-1 flex justify-end items-center">
                <Button
                    size="icon"
                    variant="destructive"
                    @click.prevent="() => deleteCombatMutation.mutateAsync()">
                    <FontAwesomeIcon :icon="faTrash" />
                </Button>
            </div>
        </div>
        <Card
            v-for="stage in draftCombatQuery.data.value!.stages"
            :key="stage.id"
            class="border-2 p-2 border-primary/50">
            <CampaignCombatDraftStageDisplay
                :allStages="draftCombatQuery.data.value!.stages"
                :stage="stage"
                :updateStage="
                    (req) =>
                        updateStage.mutateAsync({
                            stageId: stage.id,
                            name: req.name,
                        })
                "
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
            @click="onAddStage">
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
    import {
        faArrowLeft,
        faPlusCircle,
        faTrash,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useMutation, useQuery, useQueryClient } from "@tanstack/vue-query";
    import { toTypedSchema } from "@vee-validate/zod";
    import { watchOnce } from "@vueuse/core";
    import { useForm } from "vee-validate";
    import { toast } from "vue-sonner";
    import { z } from "zod";
    import type { CreatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
    import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
    import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
    import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
    import type { UpdatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/updatePlannedCombatStageRequest";
    import { combatQueries } from "~/utils/queries/combats";
    import {
        stagedCharacterValidator,
        type DraftCombatStage,
    } from "~/utils/types/models";

    const screenSize = useScreenSize();
    const queryClient = useQueryClient();
    const api = useApi();
    const route = useRoute("app-campaigns-id-combats-drafts-draftCombatId");

    definePageMeta({
        layout: "campaign-combats",
        requiresAuth: true,
        layoutTransition: false,
        middleware: [
            async (to) => {
                if (import.meta.server) return;
                
            },
        ],
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
    function onAddStage() {
        const stages = draftCombatQuery.data.value!.stages;
        const filteredStages = stages
            .map((stage) => stage.name)
            .filter((name) => name.startsWith("Stage "));

        if (filteredStages.length === 0) {
            return addStage.mutateAsync({
                name: "Stage 1",
            });
        }

        const availableNumbers = filteredStages
            .map((name) => parseInt(name.split(" ")[1]))
            .filter((number) => !Number.isNaN(number));

        if (availableNumbers.length === 0) {
            return addStage.mutateAsync({
                name: "Stage 1",
            });
        }

        const biggestNumber = availableNumbers.sort((a, b) => -(a - b))[0];

        return addStage.mutateAsync({
            name: `Stage ${biggestNumber + 1}`,
        });
    }

    const addNpc = useMutation({
        mutationFn: async (req: {
            stage: DraftCombatStage;
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
            stage: DraftCombatStage;
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
            stage: DraftCombatStage;
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
        mutationFn: async (stage: DraftCombatStage) =>
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
        mutationFn: async (req: { stageId: string; name: string }) => {
            return await api.draftCombat.stage.update({
                combatId: route.params.draftCombatId,
                stageId: req.stageId,
                name: req.name,
            });
        },
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

    const updateDraftCombatName = useMutation({
        mutationFn: async (name: string) => {
            return await api.draftCombat.update({
                plannedCombatId: route.params.draftCombatId,
                combatName: name,
            });
        },
        onSuccess(resp) {
            queryClient.setQueryData(
                combatQueries.getDraftCombat.key(
                    route.params.id,
                    route.params.draftCombatId
                ),
                resp
            );
            queryClient.invalidateQueries({
                queryKey: [route.params.id, "combats", "all"],
            });
        },
    });
    
    const updateCombatName = useDebouncedAsyncFn(async (name: string) => {
        return await updateDraftCombatName
            .mutateAsync(name)
            .then(() => toast.success("Updated combat name"))
            .catch(() => toast.error("Failed to update combat name"));
    });
    const form = useForm({
        validationSchema: toTypedSchema(
            z.object({ name: z.string().nonempty() })
        ),
    });
    const submitUpdateCombatName = form.handleSubmit(async (formValue) => {
        await updateCombatName.debouncedSubmit(formValue.name);
    });

    const [combatName] = form.defineField("name");

    watch(draftCombatQuery.data, () => {
        if (draftCombatQuery.data.value) {
            console.log(draftCombatQuery.data.value?.combatName);
            form.resetForm({
                values: {
                    name: draftCombatQuery.data.value?.combatName ?? "",
                },
            });
        }
    });

    const deleteCombatMutation = useMutation({
        mutationFn: async () => {
            return await api.draftCombat.delete({
                campaignId: route.params.id,
                combatId: route.params.draftCombatId,
            });
        },
        async onSuccess() {
            queryClient.invalidateQueries({
                queryKey: [route.params.id, "combats", "all"],
            });
            await navigateTo({
                name: "app-campaigns-id-combats",
                params: { id: route.params.id },
            });
        },
    });
</script>
