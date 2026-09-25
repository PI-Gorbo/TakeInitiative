<template>
    <article
        ref="card"
        :id="`note-${note.id}`"
        :data-note-id="note.id"
        :aria-busy="pending || undefined"
        :class="[
            'group relative flex flex-col gap-0.5 px-4 py-1.5 transition-colors duration-700 hover:bg-accent/30',
            note.isRecap && 'border-l-2 border-gold bg-gold/5 pl-[14px]',
            highlighted && '!bg-gold/15',
            // A long-press opens the action sheet, not the system's selection or callout.
            // Never while editing: iOS will not type into a field inside `user-select: none`.
            !editing && '[-webkit-touch-callout:none] [@media(pointer:coarse)]:select-none',
        ]">
        <header class="flex flex-wrap items-baseline gap-x-2 gap-y-0.5 text-sm">
            <!-- On a timeline (15c): the session number, linking to the note in the stream. -->
            <NuxtLink
                v-if="timeline"
                :to="noteHref"
                class="-my-2 flex min-h-11 items-center rounded font-semibold text-gold hover:underline md:min-h-0"
                :aria-label="`Session ${timeline.sessionNumber}: open this note in the session stream`">
                S{{ timeline.sessionNumber }}
            </NuxtLink>
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
                {{ timeline ? formatSessionDate(note.postedAt) : formatNoteTime(note.postedAt) }}
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
                v-if="note.addedLater && session"
                class="text-xs italic text-muted-foreground">
                {{ addedLaterLabel(session!.startedAt, note.postedAt) }}
            </span>
            <span
                v-if="note.isHidden"
                class="flex items-center gap-1 text-xs text-destructive-tint">
                <EyeOff
                    class="size-3"
                    aria-hidden="true" />
                Hidden by a DM
            </span>
            <span
                v-if="pending"
                class="text-xs text-muted-foreground">
                Sending…
            </span>
            <!-- The note's menu. A long-press on a touch screen asks the stream for the
                 action sheet with the same actions (14e). -->
            <slot
                name="actions"
                :actions="actions"
                :run="run">
                <SessionNoteActions
                    v-if="!editing && actions.length > 0"
                    :note="note"
                    :actions="actions"
                    @select="run" />
            </slot>
        </header>
        <SessionNoteEditor
            v-if="editing"
            :campaignId="campaignId"
            :note="note"
            :viewer="{ memberId: currentMemberId, isDm }"
            @saved="editing = false"
            @cancel="editing = false" />
        <template v-else>
            <!-- Images first, then the caption under them (16c). -->
            <ImageNoteImages
                v-if="note.images.length > 0"
                :campaignId="campaignId"
                :images="note.images"
                :text="note.text"
                :authorName="authorName"
                :compact="!!timeline"
                :class="['my-0.5', (note.isHidden || pending) && 'opacity-60']"
                @open="(imageId) => emit('openImage', imageId)" />
            <SessionNoteMarkdown
                v-if="note.text"
                :campaignId="campaignId"
                :text="note.text"
                :class="(note.isHidden || pending) && 'opacity-60'" />
            <!-- The note-level half of a loose end (§5): the author resolves it. -->
            <button
                v-if="tagHint"
                type="button"
                class="-my-2 flex min-h-11 w-fit items-center gap-1 rounded px-1 text-xs text-gold hover:underline md:min-h-0 md:py-1"
                @click="editing = true">
                <span aria-hidden="true">⚠</span>
                {{ IMAGE_MESSAGES.tagHint }}
            </button>
        </template>

        <WikiPromoteDialog
            v-if="promoteOpened"
            v-model:open="promoteOpen"
            :campaignId="campaignId"
            :note="note"
            :viewer="{ memberId: currentMemberId, isDm }"
            :entryId="promoteEntryId" />

        <SessionNoteHistoryDialog
            v-if="historyOpened"
            v-model:open="historyOpen"
            :campaignId="campaignId"
            :noteId="note.id" />

        <Dialog
            v-if="deleteAsked"
            v-model:open="confirmDelete">
            <DialogContent class="max-w-sm">
                <DialogHeader>
                    <DialogTitle>Delete this note?</DialogTitle>
                    <DialogDescription>It is removed for everyone. This cannot be undone.</DialogDescription>
                </DialogHeader>
                <DialogFooter class="gap-2">
                    <Button
                        variant="ghost"
                        class="h-11 md:h-9"
                        @click="confirmDelete = false">
                        Cancel
                    </Button>
                    <Button
                        variant="destructive"
                        class="h-11 md:h-9"
                        :disabled="deleteNote.isPending.value"
                        @click="remove">
                        Delete
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    </article>
</template>

<script setup lang="ts">
    import { EyeOff } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import type { Session, SessionNote, Visibility } from "~/utils/api/types";
    import { NOTE_LINK_PARAM, noteActionsFor, noteLink, type NoteAction } from "~/utils/noteActions";
    import {
        deleteNoteMutation,
        putNoteHiddenMutation,
        putNoteVisibilityMutation,
    } from "~/utils/queries/sessions";
    import { addedLaterLabel, formatNoteDateTime, formatNoteTime, formatSessionDate } from "~/utils/sessionDates";
    import { isPendingNote } from "~/utils/sessionStreamCache";
    import { IMAGE_MESSAGES, showTagHint } from "~/utils/images";

    const emit = defineEmits<{
        /** An image was tapped: the stream or timeline opens the viewer (16c). */
        openImage: [imageId: string];
        /** A long-press: the stream opens `NoteActionSheet` with these. */
        openActions: [payload: { actions: NoteAction[]; run: (action: NoteAction, visibility?: Visibility) => Promise<void> }];
    }>();

    const props = defineProps<{
        campaignId: string;
        note: SessionNote;
        /** The note's session, in the stream. */
        session?: Session;
        /**
         * The compact variant on an entry's timeline (15c): the session number links to
         * the note in the stream, the time shows its date, and the note is read-only
         * here apart from Promote (15f), which the timeline draws in the actions slot.
         */
        timeline?: { sessionNumber: number };
        /** Promote into this entry: the timeline's `[Promote]`, when the viewer can edit it (15f). */
        promoteEntryId?: string;
        authorName: string;
        /** The viewer's member id, to tell whether they wrote the note. */
        currentMemberId: string;
        isDm: boolean;
        /** Briefly true after following a link to this note. */
        highlighted?: boolean;
    }>();

    const visibilityTitle = computed(() =>
        props.note.visibility === "DM" ? "Visible to the DMs and the author" : "Visible only to the author"
    );

    const pending = computed(() => isPendingNote(props.note.id));
    // "⚠ Tag what's in this?": the author's own image note with no mention, in the stream.
    const tagHint = computed(
        () => !props.timeline && !pending.value && showTagHint(props.note, props.currentMemberId)
    );
    const noteHref = computed(
        () => `/app/campaigns/${encodeURIComponent(props.campaignId)}?${NOTE_LINK_PARAM}=${encodeURIComponent(props.note.id)}`
    );
    const actions = computed<NoteAction[]>(() =>
        props.timeline
            ? props.promoteEntryId && !isPendingNote(props.note.id)
                ? ["promote"]
                : []
            : noteActionsFor(props.note, {
                  isAuthor: props.note.authorMemberId === props.currentMemberId,
                  isDm: props.isDm,
              })
    );

    const editing = ref(false);

    useLongPress(
        useTemplateRef<HTMLElement>("card"),
        () => emit("openActions", { actions: actions.value, run }),
        { disabled: () => editing.value || actions.value.length === 0 || !!props.timeline }
    );
    const promoteOpen = ref(false);
    // Mounted on first open only, like the history dialog.
    const promoteOpened = ref(false);
    const historyOpen = ref(false);
    // Mounted on first open only, so the stream does not hold a dialog per note.
    const historyOpened = ref(false);
    const confirmDelete = ref(false);
    const deleteAsked = ref(false);

    const putVisibility = putNoteVisibilityMutation();
    const putHidden = putNoteHiddenMutation();
    const deleteNote = deleteNoteMutation();

    const ids = () => ({ campaignId: props.campaignId, noteId: props.note.id });

    /** Carries out a note action, from the menu here or 14e's action sheet. */
    async function run(action: NoteAction, visibility?: Visibility) {
        try {
            switch (action) {
                case "promote":
                    promoteOpened.value = true;
                    promoteOpen.value = true;
                    break;
                case "edit":
                    editing.value = true;
                    break;
                case "visibility":
                    if (visibility && visibility !== props.note.visibility) {
                        await putVisibility.mutateAsync({ ...ids(), visibility });
                    }
                    break;
                case "hide":
                case "unhide":
                    await putHidden.mutateAsync({ ...ids(), hidden: action === "hide" });
                    break;
                case "history":
                    historyOpened.value = true;
                    historyOpen.value = true;
                    break;
                case "copyLink":
                    await copyLink();
                    break;
                case "delete":
                    deleteAsked.value = true;
                    confirmDelete.value = true;
                    break;
            }
        } catch (error) {
            toast.error(apiErrorMessage(error, "That did not work. Try again."));
        }
    }

    async function remove() {
        // Close first: a successful delete unmounts this card and its dialog.
        confirmDelete.value = false;
        try {
            await deleteNote.mutateAsync(ids());
        } catch (error) {
            toast.error(apiErrorMessage(error, "Could not delete the note."));
        }
    }

    async function copyLink() {
        const link = noteLink(window.location.origin, props.campaignId, props.note.id);
        try {
            await navigator.clipboard.writeText(link);
            toast.success("Link copied.");
        } catch {
            toast.error("Could not copy the link.");
        }
    }
</script>
