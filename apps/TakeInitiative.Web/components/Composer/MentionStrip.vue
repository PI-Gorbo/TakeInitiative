<template>
    <!-- The `@` suggestions (15d, design §3 and §3a). On a phone this is the mention
         strip: one row of chips between the text box and the toolbar, so it stays above
         the keyboard with the pinned composer. `docked` pins it above the keyboard by
         itself, for an editor that is not pinned (15f's article editor). From md it is a popover at the caret. Every control uses
         `mousedown.prevent`, so the text box keeps focus and the keyboard stays up. -->
    <div
        v-if="picker.open.value"
        :class="[
            'flex flex-col gap-1 md:absolute md:z-20 md:w-72 md:rounded-md md:border md:bg-popover md:p-1 md:shadow-md',
            docked && 'fixed inset-x-0 z-40 border-t bg-background px-2 py-1 px-safe md:inset-x-auto',
        ]"
        :style="style">
        <div
            :id="listId"
            role="listbox"
            aria-label="Entries to mention"
            class="flex gap-1 overflow-x-auto md:flex-col md:overflow-visible">
            <button
                v-for="(suggestion, index) in picker.suggestions.value"
                :id="`${listId}-${index}`"
                :key="suggestion.kind === 'entry' ? suggestion.entryId : 'create'"
                type="button"
                role="option"
                :aria-selected="index === picker.highlighted.value"
                :class="[
                    'flex h-11 shrink-0 items-center gap-1.5 whitespace-nowrap rounded-md border px-3 text-sm md:h-auto md:min-h-8 md:border-0 md:px-2 md:py-1.5 md:text-left',
                    index === picker.highlighted.value
                        ? 'border-gold/60 md:bg-accent md:text-accent-foreground'
                        : 'hover:bg-accent/60',
                ]"
                @mousedown.prevent
                @click="picker.pick(index)">
                <template v-if="suggestion.kind === 'entry'">
                    <span aria-hidden="true">{{ ENTRY_KIND_ICONS[suggestion.entryKind] }}</span>
                    <span class="max-w-48 truncate font-medium">{{ suggestion.name }}</span>
                    <span
                        v-if="suggestion.alias"
                        class="max-w-32 truncate text-xs text-muted-foreground"
                        >· aka {{ suggestion.alias }}</span
                    >
                    <span
                        v-if="suggestion.isNew"
                        class="rounded bg-gold/15 px-1 text-xs text-gold"
                        >new</span
                    >
                </template>
                <template v-else>
                    <Plus
                        class="size-4 shrink-0"
                        aria-hidden="true" />
                    <span class="max-w-32 truncate md:max-w-48">Create "{{ suggestion.name }}"</span>
                    <!-- The kind, inline with Create (design §3a): a tap takes the next of the
                         six, round and round; Tab does the same on a keyboard. -->
                    <span
                        class="ml-auto flex h-9 shrink-0 items-center gap-1 rounded-md border bg-muted/60 px-2 text-xs text-muted-foreground md:h-auto md:bg-transparent md:px-1.5"
                        :title="kindHint"
                        :aria-label="kindHint"
                        @mousedown.prevent
                        @click.stop="picker.cycleKind(1)">
                        <span aria-hidden="true">{{ ENTRY_KIND_ICONS[picker.kind.value] }}</span>
                        {{ picker.kind.value }}
                        <ChevronRight
                            class="size-3 shrink-0 md:hidden"
                            aria-hidden="true" />
                        <span
                            class="hidden md:inline"
                            aria-hidden="true"
                            >⇥</span
                        >
                    </span>
                </template>
            </button>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { useMediaQuery } from "@vueuse/core";
    import { ChevronRight, Plus } from "lucide-vue-next";
    import type { MentionPicker } from "~/composables/useMentionPicker";
    import { ENTRY_KIND_ICONS } from "~/utils/entries";

    const props = withDefaults(
        defineProps<{
            picker: MentionPicker;
            listId: string;
            /** Pin above the keyboard on a phone by itself (an editor that is not pinned). */
            docked?: boolean;
            /** The desktop popover opens above the caret (the composer) or below it (an editor). */
            placement?: "above" | "below";
        }>(),
        { docked: false, placement: "above" }
    );

    const desktop = useMediaQuery("(min-width: 768px)");
    const inset = useKeyboardInset();

    const kindHint = computed(
        () => `Kind: ${props.picker.kind.value}. Tap to change it, or Tab on a keyboard.`
    );

    const style = computed(() => {
        const anchor = props.picker.anchor.value;
        if (desktop.value) {
            if (!anchor) return undefined;
            // Kept inside the text box's width; 18rem is the popover's width.
            const left = `max(0px, min(${anchor.left}px, calc(100% - 18rem)))`;
            return props.placement === "above"
                ? { left, top: `${anchor.top - 4}px`, transform: "translateY(-100%)" }
                : { left, top: `${anchor.top + anchor.lineHeight + 4}px` };
        }
        if (!props.docked) return undefined;
        return {
            bottom: `${inset.value}px`,
            paddingBottom: inset.value > 0 ? undefined : "calc(0.25rem + env(safe-area-inset-bottom))",
        };
    });
</script>
