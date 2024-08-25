<template>
    <div
        class="flex h-full items-center justify-center text-5xl"
        v-if="status == 'error' || status == 'pending'"
    >
        <FontAwesomeIcon icon="spinner" class="fa-spin" />
    </div>
    <div v-else>
        <template v-if="state.view == 'Events'">
            <CombatHistoryEventsDisplay :historyResponse="data!" />
        </template>
        <template v-else> </template>
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { GetCombatsResponse } from "base/utils/api/combat/getCombatsRequest";

const props = withDefaults(
    defineProps<{
        combatInfo: GetCombatsResponse["combats"][number];
    }>(),
    {},
);

const { data, error, status } = useAsyncData(
    "combatHistory",
    () =>
        useApi().combat.history({
            combatId: props.combatInfo.combatId,
        }),
    { watch: [() => props.combatInfo.combatId] },
);

const state = reactive<{
    view: "Events" | "Turns";
}>({
    view: "Events",
});
</script>
