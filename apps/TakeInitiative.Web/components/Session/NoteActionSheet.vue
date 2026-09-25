<template>
    <!-- A note's actions on a touch screen (14e, design §3a): long-press a note to open
         this sheet. It draws the same `noteActionsFor` list as the desktop menu and
         hands the choice back to the card. Opening it moves focus into the sheet, so
         the on-screen keyboard closes and never covers it (invariant 11). -->
    <Sheet v-model:open="open">
        <SheetContent
            side="bottom"
            class="flex max-h-[85dvh] flex-col gap-0 rounded-t-xl p-0 pb-safe [&>button:last-child]:hidden"
            @closeAutoFocus="onClosed">
            <SheetHeader class="space-y-0.5 border-b px-4 py-3 text-left">
                <SheetTitle class="text-base">Note by {{ authorName }}</SheetTitle>
                <SheetDescription class="line-clamp-2 text-sm">{{ preview }}</SheetDescription>
            </SheetHeader>

            <ul class="flex min-h-0 flex-col overflow-y-auto py-1">
                <template
                    v-for="action in actions"
                    :key="action">
                    <li v-if="action === 'visibility'">
                        <button
                            type="button"
                            :aria-expanded="showVisibility"
                            class="flex min-h-12 w-full items-center gap-3 px-4 text-left hover:bg-accent"
                            @click="showVisibility = !showVisibility">
                            <Eye
                                class="size-5 text-muted-foreground"
                                aria-hidden="true" />
                            <span class="flex-1">{{ NOTE_ACTION_LABELS.visibility }}</span>
                            <ChevronDown
                                :class="['size-4 text-muted-foreground transition-transform', showVisibility && 'rotate-180']"
                                aria-hidden="true" />
                        </button>
                        <div
                            v-if="showVisibility"
                            role="radiogroup"
                            :aria-label="NOTE_ACTION_LABELS.visibility"
                            class="flex flex-col pb-1 pl-12">
                            <button
                                v-for="option in VISIBILITY_OPTIONS"
                                :key="option.value"
                                type="button"
                                role="radio"
                                :aria-checked="note.visibility === option.value"
                                class="flex min-h-12 items-center gap-3 pr-4 text-left hover:bg-accent"
                                @click="choose('visibility', option.value)">
                                <span class="flex flex-1 flex-col">
                                    <span>{{ option.label }}</span>
                                    <span class="text-xs text-muted-foreground">{{ option.hint }}</span>
                                </span>
                                <Check
                                    v-if="note.visibility === option.value"
                                    class="size-4 text-gold"
                                    aria-hidden="true" />
                            </button>
                        </div>
                    </li>
                    <li v-else>
                        <button
                            type="button"
                            :class="[
                                'flex min-h-12 w-full items-center gap-3 px-4 text-left hover:bg-accent',
                                action === 'delete' && 'text-destructive-tint',
                            ]"
                            @click="choose(action)">
                            <component
                                :is="ICONS[action]"
                                :class="['size-5', action !== 'delete' && 'text-muted-foreground']"
                                aria-hidden="true" />
                            {{ NOTE_ACTION_LABELS[action] }}
                        </button>
                    </li>
                </template>
            </ul>

            <div class="border-t p-2">
                <SheetClose
                    class="flex h-12 w-full items-center justify-center rounded-md text-sm font-medium hover:bg-accent">
                    Cancel
                </SheetClose>
            </div>
        </SheetContent>
    </Sheet>
</template>

<script setup lang="ts">
    import { Check, ChevronDown, Eye, EyeOff, History, Link, Pencil, Trash2 } from "lucide-vue-next";
    import type { Component } from "vue";
    import type { SessionNote, Visibility } from "~/utils/api/types";
    import { VISIBILITY_OPTIONS } from "~/utils/composer";
    import { NOTE_ACTION_LABELS, type NoteAction } from "~/utils/noteActions";

    const props = defineProps<{
        note: SessionNote;
        actions: NoteAction[];
        authorName: string;
    }>();
    const open = defineModel<boolean>("open", { required: true });
    const emit = defineEmits<{ select: [action: NoteAction, visibility?: Visibility] }>();

    const showVisibility = ref(false);
    watch(open, (value) => {
        if (value) showVisibility.value = false;
    });

    const preview = computed(() => {
        const text = props.note.text.replace(/\s+/g, " ").trim();
        return text.length > 140 ? `${text.slice(0, 140)}…` : text;
    });

    // Edit, Edit history and Delete run once the sheet has closed and let go of focus,
    // so the editor or dialog they open keeps the focus it takes. The rest run at once:
    // Copy link must write the clipboard inside the tap (iOS refuses it later).
    const AFTER_CLOSE: readonly NoteAction[] = ["edit", "history", "delete"];
    let pending: [NoteAction, Visibility?] | null = null;
    let fallback: ReturnType<typeof setTimeout> | undefined;

    function choose(action: NoteAction, visibility?: Visibility) {
        open.value = false;
        if (!AFTER_CLOSE.includes(action)) {
            emit("select", action, visibility);
            return;
        }
        pending = [action, visibility];
        // In case the close event never comes (the sheet unmounted mid-animation).
        clearTimeout(fallback);
        fallback = setTimeout(flush, 600);
    }

    function flush() {
        clearTimeout(fallback);
        const chosen = pending;
        pending = null;
        if (chosen) emit("select", ...chosen);
    }

    function onClosed(event: Event) {
        if (pending) event.preventDefault();
        flush();
    }

    onBeforeUnmount(flush);

    const ICONS: Record<NoteAction, Component> = {
        edit: Pencil,
        visibility: Eye,
        hide: EyeOff,
        unhide: Eye,
        history: History,
        copyLink: Link,
        delete: Trash2,
    };
</script>
