<template>
    <!-- Editing a note in place (author only). Enter saves on desktop, Esc cancels. The
         text reads mentions as `@[Name]`, like the composer (15d), with the same `@`
         picker: a popover below the caret from md, and on a phone the mention strip
         docked above the keyboard. -->
    <form
        ref="form"
        class="relative flex flex-col gap-1 py-1"
        aria-label="Edit note"
        @submit.prevent="save"
        @dragover="onDragOver"
        @dragleave="onDragLeave"
        @drop="onDrop">
        <div
            v-if="dragging"
            class="pointer-events-none absolute inset-0 z-10 flex items-center justify-center rounded-md border-2 border-dashed border-gold bg-background/90 text-sm font-medium text-gold">
            Drop images to attach them
        </div>
        <div class="relative flex flex-col gap-1">
            <textarea
                ref="textarea"
                v-model="state.text"
                aria-label="Note text"
                :enterkeyhint="touch ? 'enter' : 'done'"
                :placeholder="attachments.length > 0 ? IMAGE_MESSAGES.captionPlaceholder : undefined"
                :aria-controls="mentions.open.value ? `${id}-mentions` : undefined"
                :aria-activedescendant="mentions.open.value ? `${id}-mentions-${mentions.highlighted.value}` : undefined"
                class="max-h-[50dvh] min-h-11 w-full resize-none overflow-y-auto rounded-md border bg-background px-3 py-2 text-base leading-snug outline-none focus-visible:ring-1 focus-visible:ring-ring md:text-sm"
                @keydown="onKeydown"
                @input="grow"
                @paste="onPaste" />
            <ComposerMentionStrip
                :picker="mentions"
                :listId="`${id}-mentions`"
                placement="below"
                docked />
            <ComposerMentionLinks
                :state="state"
                :directory="mentions.directory.value" />
            <!-- The note's images (16c): remove, move and add. -->
            <ImageAttachmentStrip
                :campaignId="campaignId"
                :attachments="attachments"
                movable
                @remove="uploads.remove"
                @retry="uploads.retry"
                @move="uploads.move">
                <p
                    v-if="removedCount > 0"
                    class="text-xs text-muted-foreground">
                    {{
                        removedCount === 1
                            ? IMAGE_MESSAGES.willBeDeleted
                            : `${removedCount} images will be deleted.`
                    }}
                </p>
            </ImageAttachmentStrip>
            <p
                v-if="attachments.length === 0 && removedCount > 0"
                class="text-xs text-muted-foreground">
                {{ removedCount === 1 ? IMAGE_MESSAGES.willBeDeleted : `${removedCount} images will be deleted.` }}
            </p>
        </div>
        <input
            ref="fileInput"
            type="file"
            accept="image/*"
            multiple
            hidden
            @change="onFilesPicked" />
        <div class="flex flex-wrap items-center gap-1">
            <button
                type="button"
                title="Mention an entry"
                aria-label="Mention an entry"
                class="flex h-11 min-w-11 items-center justify-center rounded-md px-2 text-muted-foreground hover:bg-accent hover:text-accent-foreground md:h-8 md:min-w-8"
                @mousedown.prevent
                @click="mentions.trigger()">
                <AtSign
                    class="size-4"
                    aria-hidden="true" />
            </button>
            <button
                type="button"
                title="Attach images"
                aria-label="Attach images"
                class="flex h-11 min-w-11 items-center justify-center rounded-md px-2 text-muted-foreground hover:bg-accent hover:text-accent-foreground md:h-8 md:min-w-8"
                @mousedown.prevent
                @click="fileInput?.click()">
                <ImagePlus
                    class="size-4"
                    aria-hidden="true" />
            </button>
            <button
                type="button"
                :aria-pressed="isRecap"
                :class="[
                    'flex h-11 items-center gap-1 rounded-md px-2 text-sm md:h-8',
                    isRecap ? 'bg-gold/15 text-gold' : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
                ]"
                @click="isRecap = !isRecap">
                <ScrollText
                    class="size-4"
                    aria-hidden="true" />
                Recap
            </button>
            <span
                v-if="overLimit"
                class="text-xs text-destructive-tint">
                {{ storedLength.toLocaleString() }} / {{ NOTE_TEXT_MAX.toLocaleString() }}
            </span>
            <div class="flex-1" />
            <span class="hidden text-xs text-muted-foreground md:inline">Esc to cancel · Enter to save</span>
            <Button
                type="button"
                variant="ghost"
                class="h-11 md:h-8"
                @click="emit('cancel')">
                Cancel
            </Button>
            <Button
                type="submit"
                class="h-11 md:h-8"
                :disabled="!canSave || saving || uploads.busy.value">
                <LoaderCircle
                    v-if="saving || uploads.busy.value"
                    class="animate-spin"
                    aria-hidden="true" />
                Save
            </Button>
        </div>
        <ComposerRevealDialog
            ref="reveal"
            :campaignId="campaignId" />
    </form>
</template>

<script setup lang="ts">
    import { useQueryClient } from "@tanstack/vue-query";
    import { useMediaQuery } from "@vueuse/core";
    import { AtSign, ImagePlus, LoaderCircle, ScrollText } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import type { SessionNote, Visibility } from "~/utils/api/types";
    import { NOTE_TEXT_MAX, enterAction, postableText } from "~/utils/composer";
    import {
        IMAGE_MESSAGES,
        attachmentsFromImages,
        failUploads,
        imageFiles,
        imageIdsErrorFrom,
        imagesChanged,
        removedNoteImages,
        type Attachment,
    } from "~/utils/images";
    import type { EntryViewer } from "~/utils/entries";
    import {
        fromStoredText,
        mentionBody,
        newEntryErrorFrom,
        relinkNewEntry,
        revealCheck,
        toStoredText,
        type MentionText,
        type RevealItem,
    } from "~/utils/mentions";
    import { invalidateEntries } from "~/utils/queries/entries";
    import { invalidateSessionStreams, putNoteMutation } from "~/utils/queries/sessions";

    const props = defineProps<{
        campaignId: string;
        note: SessionNote;
        viewer: EntryViewer;
    }>();
    const emit = defineEmits<{ saved: []; cancel: [] }>();

    const id = useId();
    // The stored form back to `@[Name]` plus links (15d).
    const state = reactive<MentionText>({ ...fromStoredText(props.note.text), newEntries: [] });
    const isRecap = ref(props.note.isRecap);
    const storedLength = computed(() => toStoredText(state.text, state.links).trim().length);
    // The note's images (16c): the ones on it are removed on save; new ones upload now.
    const attachments = ref<Attachment[]>(attachmentsFromImages(props.note.images, { onNote: true }));
    const uploads = useImageAttachments({ attachments, campaignId: () => props.campaignId });
    const removedCount = computed(() => removedNoteImages(props.note.images, attachments.value));
    // With images the caption may be empty.
    const canSave = computed(
        () =>
            !uploads.failed.value &&
            (postableText(state.text) !== null || (attachments.value.length > 0 && state.text.trim() === "")) &&
            storedLength.value <= NOTE_TEXT_MAX
    );
    const overLimit = computed(() => storedLength.value > NOTE_TEXT_MAX);

    const touch = useMediaQuery("(pointer: coarse)");
    const textarea = useTemplateRef<HTMLTextAreaElement>("textarea");
    const mentions = useMentionPicker({ state, textarea, campaignId: () => props.campaignId });
    const reveal = useTemplateRef<{ confirm: (v: Visibility, items: RevealItem[]) => Promise<boolean> }>("reveal");

    const putNote = putNoteMutation();
    const queryClient = useQueryClient();
    const saving = computed(() => putNote.isPending.value);

    let saved = false;
    async function save() {
        if (!canSave.value || saving.value || uploads.busy.value) return;
        const trimmed = postableText(state.text);
        // A caption may be empty when the note keeps an image.
        const body = trimmed === null ? { text: "", newEntries: [] } : mentionBody(state, trimmed);
        // `imageIds` only when the list changed: left out, the API keeps the images.
        const imageIds = imagesChanged(props.note.images, attachments.value)
            ? uploads.images.value.map((i) => i.id)
            : undefined;
        if (
            body.text === props.note.text &&
            isRecap.value === props.note.isRecap &&
            body.newEntries.length === 0 &&
            !imageIds
        ) {
            done();
            return;
        }
        // A hidden `Everyone` note is read by the DMs and its author only.
        const audience: Visibility =
            props.note.isHidden && props.note.visibility === "Everyone" ? "DM" : props.note.visibility;
        const warn = revealCheck(audience, body.text, mentions.directory.value, props.viewer);
        if (warn.length > 0 && !(await reveal.value?.confirm(audience, warn))) return;

        try {
            await putNote.mutateAsync({
                campaignId: props.campaignId,
                noteId: props.note.id,
                text: body.text,
                isRecap: isRecap.value,
                ...(body.newEntries.length > 0 ? { newEntries: body.newEntries } : {}),
                ...(imageIds ? { imageIds } : {}),
            });
            done();
        } catch (error) {
            const entryError = newEntryErrorFrom(error);
            if (entryError?.kind === "alreadyCreated") {
                // A retry after a timeout: the first save went through.
                toast.info("That edit was already saved.");
                void invalidateSessionStreams(queryClient, props.campaignId);
                void invalidateEntries(queryClient, props.campaignId);
                done();
                return;
            }
            if (entryError?.kind === "duplicate") {
                const name = state.newEntries.find(
                    (e) => e.id.toLowerCase() === entryError.newEntryId.toLowerCase()
                )?.name;
                Object.assign(state, relinkNewEntry(state, entryError.newEntryId, entryError.existingEntryId));
                toast.error(`"${name ?? "That entry"}" already exists, so the mention now links to it. Save again.`);
                return;
            }
            // 16b's `errors.imageIds`: a new upload was swept or taken; upload it again.
            const imagesError = imageIdsErrorFrom(error);
            if (imagesError) {
                attachments.value = failUploads(attachments.value, imagesError);
                toast.error(imagesError);
                return;
            }
            toast.error(apiErrorMessage(error, "Could not save the note."));
        }
    }

    /** Saved: the new images are on the note now, so only their previews go. */
    function done() {
        saved = true;
        uploads.release(attachments.value);
        emit("saved");
    }

    // Cancelled, or the note went away: uploads the note never took are deleted.
    onBeforeUnmount(() => {
        if (!saved) uploads.discard();
    });

    // ── Adding images: 🖼, paste and drop ────────────────────────────────────
    const fileInput = useTemplateRef<HTMLInputElement>("fileInput");
    const form = useTemplateRef<HTMLFormElement>("form");
    const dragging = ref(false);
    function onFilesPicked(event: Event) {
        const input = event.target as HTMLInputElement;
        uploads.add(Array.from(input.files ?? []));
        input.value = "";
        textarea.value?.focus();
    }
    function onPaste(event: ClipboardEvent) {
        const files = imageFiles(event.clipboardData?.files);
        if (files.length === 0) return;
        event.preventDefault();
        uploads.add(files);
    }
    const carriesFiles = (event: DragEvent) => !!event.dataTransfer && Array.from(event.dataTransfer.types).includes("Files");
    function onDragOver(event: DragEvent) {
        if (!carriesFiles(event)) return;
        event.preventDefault();
        dragging.value = true;
    }
    function onDragLeave(event: DragEvent) {
        if (event.relatedTarget instanceof Node && form.value?.contains(event.relatedTarget)) return;
        dragging.value = false;
    }
    function onDrop(event: DragEvent) {
        dragging.value = false;
        if (!carriesFiles(event)) return;
        event.preventDefault();
        const files = Array.from(event.dataTransfer?.files ?? []);
        const images = imageFiles(files);
        if (images.length < files.length) toast.error(IMAGE_MESSAGES.unsupported);
        uploads.add(images);
    }

    function onKeydown(event: KeyboardEvent) {
        if (mentions.onKeydown(event)) return;
        if (event.key === "Escape") {
            event.preventDefault();
            emit("cancel");
            return;
        }
        if (enterAction(event, touch.value) === "post") {
            event.preventDefault();
            void save();
        }
    }

    function grow() {
        const el = textarea.value;
        if (!el) return;
        el.style.height = "auto";
        el.style.height = `${el.scrollHeight + (el.offsetHeight - el.clientHeight)}px`;
    }

    onMounted(() => {
        grow();
        const el = textarea.value;
        el?.focus();
        el?.setSelectionRange(el.value.length, el.value.length);
    });
</script>
