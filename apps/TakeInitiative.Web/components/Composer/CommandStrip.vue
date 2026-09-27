<template>
    <!-- The `/` command strip (14e, design §3a). On a phone it is a row of chips docked
         between the text box and the toolbar, so it stays above the keyboard with the
         composer. From md it is a popover just above the text box, where the caret is:
         a command only counts at the start of the text. -->
    <div
        v-if="suggestions.length > 0 || error"
        class="flex flex-col gap-1 md:absolute md:bottom-full md:left-0 md:z-20 md:mb-1 md:w-72 md:rounded-md md:border md:bg-popover md:p-1 md:shadow-md">
        <div
            v-if="suggestions.length > 0"
            :id="listId"
            role="listbox"
            :aria-label="suggestions[0]?.kind === 'session' ? 'Sessions' : 'Commands'"
            class="flex gap-1 overflow-x-auto md:flex-col md:overflow-visible">
            <button
                v-for="(suggestion, index) in suggestions"
                :id="`${listId}-${index}`"
                :key="key(suggestion)"
                type="button"
                role="option"
                :aria-selected="index === active"
                :class="[
                    'flex h-11 shrink-0 items-center gap-2 whitespace-nowrap rounded-md border px-3 text-sm md:h-auto md:min-h-8 md:border-0 md:px-2 md:py-1.5 md:text-left',
                    index === active ? 'md:bg-accent md:text-accent-foreground' : 'hover:bg-accent/60',
                ]"
                @mousedown.prevent
                @click="emit('pick', suggestion)">
                <template v-if="suggestion.kind === 'command'">
                    <span class="font-mono font-medium">{{ suggestion.command.usage }}</span>
                    <span class="text-xs text-muted-foreground">{{ suggestion.command.hint }}</span>
                </template>
                <template v-else>
                    <span class="font-medium">{{ suggestion.label }}</span>
                    <span
                        v-if="suggestion.session.title"
                        class="max-w-40 truncate text-xs text-muted-foreground"
                        >{{ suggestion.session.title }}</span
                    >
                </template>
            </button>
        </div>
        <p
            v-if="error"
            role="alert"
            class="px-1 text-xs text-destructive-tint">
            {{ error }}
        </p>
    </div>
</template>

<script setup lang="ts">
    import type { CommandSuggestion } from "~/utils/composerCommands";

    defineProps<{
        suggestions: CommandSuggestion[];
        active: number;
        error: string | null;
        listId: string;
    }>();
    const emit = defineEmits<{ pick: [suggestion: CommandSuggestion] }>();

    const key = (s: CommandSuggestion) => (s.kind === "command" ? `c:${s.command.name}` : `s:${s.session.id}`);
</script>
