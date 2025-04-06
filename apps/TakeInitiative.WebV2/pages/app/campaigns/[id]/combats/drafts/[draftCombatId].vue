<template>
    <LoadingFallback :isLoading="draftCombatQuery.isLoading.value">
        <div
            class="flex w-full flex-1 justify-center pb-1"
            v-for="stage in draftCombatQuery.data.value!.stages"
            :key="stage.id">
            <PlannedCombatStageDisplay
                :stage="stage"
                :updateStage="(req) => updateStage(stage, req)"
                :deleteStage="() => deleteStage(stage)"
                :createNpc="(request) => addNpc(stage, request)"
                :updateNpc="(request) => updateNpc(stage, request)"
                :deleteNpc="(request) => deleteNpc(stage, request)" />
        </div>
    </LoadingFallback>
</template>
<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { combatQueries } from "~/utils/queries/combats";

    const route = useRoute("app-campaigns-id-combats-drafts-draftCombatId");
    definePageMeta({
        layout: "campaign-combats",
        requiresAuth: true,
    });

    const draftCombatQuery = useQuery(
        combatQueries.getDraftCombatQuery(
            () => route.params.id,
            () => route.params.draftCombatId
        )
    );
</script>
