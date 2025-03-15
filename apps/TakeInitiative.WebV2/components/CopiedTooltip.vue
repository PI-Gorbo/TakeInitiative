<template>
    <TooltipProvider>
        <Tooltip :open="tooltipOpen">
            <TooltipTrigger>
                <slot name="Trigger" :copyText="copyText" />
            </TooltipTrigger>
            <TooltipContent> Copied! </TooltipContent>
        </Tooltip>
    </TooltipProvider>
</template>
<script setup lang="ts">
    import { useDebounceFn } from "@vueuse/core";

    const props = defineProps<{
        textToCopyToClipboard: string;
    }>();

    // Helper methods
    const tooltipOpen = ref(false);
    const debounceResetTooltip = useDebounceFn(
        () => (tooltipOpen.value = false),
        2000
    );
    function copyText() {
        navigator.clipboard.writeText(props.textToCopyToClipboard);
        tooltipOpen.value = true;
        debounceResetTooltip();
    }
</script>
