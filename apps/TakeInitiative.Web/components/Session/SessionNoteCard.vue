<template>
    <article
        :id="`note-${note.id}`"
        :data-note-id="note.id"
        :class="[
            'group relative flex flex-col gap-0.5 px-4 py-1.5 transition-colors hover:bg-accent/30',
            note.isRecap && 'border-l-2 border-gold bg-gold/5 pl-[14px]',
        ]">
        <header class="flex flex-wrap items-baseline gap-x-2 gap-y-0.5 text-sm">
            <span
                v-if="note.isRecap"
                class="text-xs font-semibold tracking-wide text-gold"
                >📜 RECAP</span
            >
            <span class="font-semibold">{{ authorName }}</span>
            <time
                :datetime="note.postedAt"
                :title="formatNoteDateTime(note.postedAt)"
                class="text-xs text-muted-foreground">
                {{ formatNoteTime(note.postedAt) }}
            </time>
            <span
                v-if="note.visibility !== 'Everyone'"
                class="rounded border px-1.5 text-xs font-medium text-muted-foreground"
                :title="visibilityTitle">
                🔒 {{ note.visibility }}
            </span>
            <span
                v-if="note.editedAt"
                class="text-xs text-muted-foreground"
                :title="`Edited ${formatNoteDateTime(note.editedAt)}`">
                (edited)
            </span>
            <span
                v-if="note.addedLater"
                class="text-xs italic text-muted-foreground">
                {{ addedLaterLabel(session.startedAt, note.postedAt) }}
            </span>
            <span
                v-if="note.isHidden"
                class="flex items-center gap-1 text-xs text-destructive-tint">
                <EyeOff
                    class="size-3"
                    aria-hidden="true" />
                Hidden by a DM
            </span>
            <!-- 14d: NoteActions (edit, delete, visibility, hide, history, copy link). -->
            <slot name="actions" />
        </header>
        <SessionNoteMarkdown
            :text="note.text"
            :class="note.isHidden && 'opacity-60'" />
    </article>
</template>

<script setup lang="ts">
    import { EyeOff } from "lucide-vue-next";
    import type { Session, SessionNote } from "~/utils/api/types";
    import { addedLaterLabel, formatNoteDateTime, formatNoteTime } from "~/utils/sessionDates";

    const props = defineProps<{
        note: SessionNote;
        session: Session;
        authorName: string;
    }>();

    const visibilityTitle = computed(() =>
        props.note.visibility === "DM" ? "Visible to the DMs and the author" : "Visible only to the author"
    );
</script>
