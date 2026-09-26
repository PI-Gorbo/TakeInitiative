<template>
    <!-- The caption nudge (design §3, §5): ➤ with images and no caption asks once.
         Post anyway posts; Add caption focuses the text box. -->
    <div
        role="alert"
        class="flex flex-wrap items-center gap-2 rounded-md border border-gold/40 bg-gold/5 py-1 pl-3 pr-1 text-sm">
        <span class="min-w-0 flex-1">{{ IMAGE_MESSAGES.captionNudge }}</span>
        <div class="flex shrink-0 gap-1">
            <Button
                type="button"
                size="sm"
                variant="ghost"
                class="h-11 md:h-8"
                @mousedown.prevent
                @click="emit('addCaption')">
                Add caption
            </Button>
            <Button
                type="button"
                size="sm"
                class="h-11 md:h-8"
                @mousedown.prevent
                @click="emit('postAnyway')">
                Post anyway
            </Button>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { IMAGE_MESSAGES } from "~/utils/images";

    const emit = defineEmits<{ postAnyway: []; addCaption: [] }>();
</script>
