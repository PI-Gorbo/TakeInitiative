<template>{{ data?.history }}</template>
<script setup lang="ts">
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
    { watch: [props.combatInfo] },
);
</script>
