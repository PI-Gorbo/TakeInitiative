<template>
    <!-- Promote from a selection (15f, design §4, desktop only): selecting text inside
         one note shows "Add to wiki" by the selection. `utils/promote.ts` maps the
         rendered selection back to the note's markdown. On a touch screen the note's
         long-press sheet promotes the whole note instead (§3a). One per page. -->
    <Teleport to="body">
        <button
            v-if="shown"
            type="button"
            class="fixed z-50 flex h-8 items-center gap-1 rounded-md border bg-popover px-2.5 text-sm font-medium text-popover-foreground shadow-md hover:bg-accent"
            :style="{ top: `${shown.top}px`, left: `${shown.left}px` }"
            @mousedown.prevent
            @click="promote">
            <BookPlus
                class="size-4 text-gold"
                aria-hidden="true" />
            Add to wiki
        </button>
    </Teleport>
    <WikiPromoteDialog
        v-if="dialog"
        v-model:open="dialogOpen"
        :campaignId="campaignId"
        :note="dialog.note"
        :viewer="viewer"
        :excerpt="dialog.excerpt"
        :entryId="entryId" />
</template>

<script setup lang="ts">
    import { useEventListener, useMediaQuery } from "@vueuse/core";
    import { BookPlus } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { SessionNote } from "~/utils/api/types";
    import type { EntryViewer } from "~/utils/entries";
    import { selectionToSource } from "~/utils/promote";
    import { isPendingNote } from "~/utils/sessionStreamCache";

    const props = defineProps<{
        campaignId: string;
        viewer: EntryViewer;
        /** The loaded note with this id, or undefined (not on this page). */
        findNote: (noteId: string) => SessionNote | undefined;
        /** Promote into this entry (an entry's timeline). */
        entryId?: string;
    }>();

    const desktop = useMediaQuery("(min-width: 768px) and (pointer: fine)");
    type Shown = {
        top: number;
        left: number;
        note: SessionNote;
        selected: string;
    };
    const shown = shallowRef<Shown | null>(null);

    /** The note whose rendered text holds the whole selection, if one does. */
    function selectedNote(selection: Selection): SessionNote | undefined {
        if (selection.rangeCount === 0 || selection.isCollapsed) return undefined;
        const range = selection.getRangeAt(0);
        const element = (node: Node) => (node instanceof Element ? node : node.parentElement);
        const start = element(range.startContainer)?.closest(".note-markdown");
        const end = element(range.endContainer)?.closest(".note-markdown");
        if (!start || start !== end) return undefined;
        const noteId = start.closest("[data-note-id]")?.getAttribute("data-note-id");
        const note = noteId ? props.findNote(noteId) : undefined;
        return note && !isPendingNote(note.id) ? note : undefined;
    }

    function update() {
        const selection = desktop.value ? document.getSelection() : null;
        const note = selection ? selectedNote(selection) : undefined;
        const selected = selection?.toString() ?? "";
        if (!selection || !note || selected.trim() === "") {
            shown.value = null;
            return;
        }
        const rects = selection.getRangeAt(0).getClientRects();
        const last = rects[rects.length - 1];
        if (!last) {
            shown.value = null;
            return;
        }
        // Just under the end of the selection, kept on screen.
        const top = Math.min(last.bottom + 6, window.innerHeight - 40);
        const left = Math.max(8, Math.min(last.right - 60, window.innerWidth - 130));
        shown.value = { top, left, note, selected };
    }

    if (import.meta.client) {
        useEventListener(document, "selectionchange", update);
        useEventListener(window, "scroll", update, {
            capture: true,
            passive: true,
        });
        useEventListener(window, "resize", update, { passive: true });
    }

    const dialog = shallowRef<{ note: SessionNote; excerpt: string } | null>(null);
    const dialogOpen = ref(false);

    function promote() {
        const target = shown.value;
        if (!target) return;
        const source = selectionToSource(target.note.text, target.selected);
        if (!source) {
            toast.error("Select text inside one note to add it to the wiki.");
            return;
        }
        dialog.value = { note: target.note, excerpt: source.text };
        dialogOpen.value = true;
        document.getSelection()?.removeAllRanges();
        shown.value = null;
    }
</script>
