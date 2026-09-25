<template>
    <!-- The gap prompt (glossary §1): one tap starts the next session and targets it.
         Ignoring it posts to the current session. -->
    <div
        role="status"
        class="flex items-center gap-2 rounded-md border border-gold/40 bg-gold/5 py-1 pl-3 pr-1 text-sm">
        <CalendarPlus
            class="size-4 shrink-0 text-gold"
            aria-hidden="true" />
        <span class="min-w-0 flex-1">{{ text }}</span>
        <Button
            type="button"
            size="sm"
            class="h-11 shrink-0 md:h-8"
            :disabled="starting"
            @click="emit('accept')">
            <LoaderCircle
                v-if="starting"
                class="animate-spin"
                aria-hidden="true" />
            Start Session {{ nextNumber }}
        </Button>
    </div>
</template>

<script setup lang="ts">
    import { CalendarPlus, LoaderCircle } from "lucide-vue-next";

    defineProps<{
        /** "Last note was 5 days ago. Start Session 14?" */
        text: string;
        nextNumber: number;
        starting: boolean;
    }>();
    const emit = defineEmits<{ accept: [] }>();
</script>
