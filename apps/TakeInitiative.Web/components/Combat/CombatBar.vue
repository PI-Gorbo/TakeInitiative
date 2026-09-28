<template>
    <!-- The bar (18c.4): sticky at the bottom of the page, above the tab bar on a phone and
         above the home indicator on desktop. End turn, for the turn's owner and the DMs. -->
    <div
        class="sticky bottom-0 z-10 border-t bg-background/95 px-3 py-2 backdrop-blur md:pb-safe">
        <div class="mx-auto flex w-full max-w-3xl items-center gap-3">
            <span
                v-if="state.yourTurn"
                class="shrink-0 rounded-full bg-gold px-2.5 py-1 text-xs font-semibold text-gold-foreground">
                Your turn
            </span>
            <span
                v-else-if="!state.enabled"
                class="min-w-0 flex-1 truncate text-sm text-muted-foreground"
                aria-live="polite">
                {{ state.label }}
            </span>
            <Button
                v-if="state.enabled"
                class="h-11 min-w-0 flex-1 gap-2 md:ml-auto md:flex-none"
                :disabled="pending"
                @click="emit('endTurn')">
                <SkipForward
                    class="size-4 shrink-0"
                    aria-hidden="true" />
                <span class="truncate">{{ state.label }}</span>
            </Button>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { SkipForward } from "lucide-vue-next";
    import type { EndTurnState } from "~/utils/combat";

    defineProps<{ state: EndTurnState; pending: boolean }>();
    const emit = defineEmits<{ endTurn: [] }>();
</script>
