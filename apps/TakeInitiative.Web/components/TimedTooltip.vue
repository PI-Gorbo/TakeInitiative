<template>
    <div
        :class="['tooltip', tooltip && 'tooltip-open']"
        :data-tip="tooltip"
        @click="onClick"
    >
        <slot />
    </div>
</template>
<script setup lang="ts">
import { debounce } from "base/utils/debounce";
const props = withDefaults(
    defineProps<{
        tooltip: string;
    }>(),
    {},
);
const debouncedResetTooltip = debounce(() => (tooltip.value = null), 2000);
const tooltip = ref<string | null>(null);
function onClick() {
    tooltip.value = props.tooltip;
    debouncedResetTooltip();
}
</script>
