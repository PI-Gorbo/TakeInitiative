<template>
    <!-- The bar (18c.4): at the bottom of the combat page, which keeps it sticky, above the
         tab bar on a phone and above the home indicator on desktop. End turn, for the
         turn's owner and the DMs, and ✎ for the slim composer (18e.5). -->
    <div class="border-t bg-background/95 px-3 py-2 backdrop-blur md:pb-safe">
        <div class="mx-auto flex w-full max-w-3xl items-center gap-3">
            <Button
                variant="outline"
                class="size-11 shrink-0 p-0"
                :aria-label="composing ? 'Close the note' : 'Write a session note'"
                :aria-pressed="composing"
                @click="emit('compose')">
                <X
                    v-if="composing"
                    class="size-4"
                    aria-hidden="true" />
                <Pencil
                    v-else
                    class="size-4"
                    aria-hidden="true" />
            </Button>
            <template v-if="state">
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
            </template>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { Pencil, SkipForward, X } from "lucide-vue-next";
    import type { EndTurnState } from "~/utils/combat";

    defineProps<{
        /** End turn's state; null when there is no turn to end (a Draft, or a finished combat). */
        state: EndTurnState | null;
        pending: boolean;
        /** Whether the slim composer is open above the bar. */
        composing: boolean;
    }>();
    const emit = defineEmits<{ endTurn: []; compose: [] }>();
</script>
