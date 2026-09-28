<template>
    <!-- The composer (glossary §1, design §3). It sits below the stream, outside its
         scroller. While its text box has focus on a phone it is pinned directly above
         the on-screen keyboard (design §3a, invariant 11), and this wrapper keeps its
         place, plus the keyboard's height, so the stream ends where the composer starts. -->
    <div
        class="shrink-0"
        :style="pinned ? { height: `${formHeight + inset}px` } : undefined">
        <!-- No session yet (step 17): a campaign has none until a member starts Session
             1, and a note needs one, so the composer is only this call to action until
             then. No text box, toolbar, pickers or images: there is nothing to post to,
             and a composer that cannot post only invites a note that goes nowhere. Its
             button is the gap prompt's, so the session starts the same way, and the full
             composer takes its place as soon as the session list has one. This is the
             one place that says "No sessions yet"; the stream's empty state above only
             points here. Shown only once the list has *answered* with none, so an
             ordinary load never flashes it. -->
        <section
            v-if="noSession"
            class="flex flex-col items-center gap-3 border-t bg-background px-4 py-6 text-center"
            aria-label="Start a session">
            <p class="text-sm font-medium">No sessions yet.</p>
            <Button
                type="button"
                size="lg"
                class="h-11"
                :disabled="startSession.isPending.value"
                @click="start">
                <LoaderCircle
                    v-if="startSession.isPending.value"
                    class="animate-spin"
                    aria-hidden="true" />
                <CalendarPlus
                    v-else
                    aria-hidden="true" />
                Start Session {{ nextNumber }}
            </Button>
        </section>
        <form
            v-else
            ref="form"
            :class="[
                'flex flex-col gap-1 border-t bg-background px-2 pb-2 pt-1',
                pinned ? 'fixed inset-x-0 z-30 px-safe' : 'relative',
            ]"
            :style="pinned ? pinnedStyle : undefined"
            aria-label="Composer"
            @submit.prevent="submit()"
            @dragover="onDragOver"
            @dragleave="onDragLeave"
            @drop="onDrop">
            <!-- Dropping files on the composer attaches them (16c, desktop). -->
            <div
                v-if="dragging"
                class="pointer-events-none absolute inset-0 z-10 flex items-center justify-center rounded-md border-2 border-dashed border-gold bg-background/90 text-sm font-medium text-gold">
                Drop images to attach them
            </div>
            <!-- Editing a note (step 17): the note is up in the composer, and this strip
                 says so. The session and visibility pickers go, since an edit cannot
                 change either (visibility has its own action on the note). -->
            <div
                v-if="editing"
                class="flex min-h-8 items-center gap-1.5 px-1 text-xs font-medium text-gold">
                <Pencil
                    class="size-3.5"
                    aria-hidden="true" />
                <span>Editing note{{ editing.sessionNumber ? ` — Session ${editing.sessionNumber}` : "" }}</span>
                <span class="ml-auto hidden font-normal text-muted-foreground md:inline">
                    Esc to cancel · Enter to save
                </span>
            </div>
            <div
                v-else
                class="flex min-w-0 items-center gap-1">
                <!-- Slim (the combat page, §3): no session picker; it posts to the current session. -->
                <ComposerSessionPicker
                    v-if="!slim"
                    v-model="state.sessionId"
                    :sessions="sessions"
                    :loaded="sessionsQuery.isSuccess.value"
                    :starting="startSession.isPending.value"
                    @start="start" />
                <ComposerVisibilityPicker v-model="state.visibility" />
            </div>

            <ComposerGapPrompt
                v-if="gapVisible && !editing && !slim"
                :text="gapText"
                :nextNumber="nextNumber"
                :starting="startSession.isPending.value"
                @accept="start" />

            <div class="relative flex flex-col gap-1">
                <!-- The text box (step 17, the rich text box): the `@` suggestions come with it, as the
                     mention strip under it on a phone (above the keyboard, where the `/`
                     strip goes) and a popover at the caret from md. The `/` strip wins
                     while it is open, so the two are never shown together. While a note is
                     being edited it writes to the edit's state, not the draft's, and it is
                     a new text box per note, so undo never reaches back into the draft. -->
                <ComposerEditor
                    ref="editor"
                    :key="editing ? `edit-${editing.note.id}` : 'draft'"
                    v-model="active.text"
                    v-model:links="active.links"
                    v-model:newEntries="active.newEntries"
                    :campaignId="campaignId"
                    :placeholder="placeholder"
                    :label="editing ? 'Edit note' : 'Session note'"
                    :describedBy="overLimit ? `${id}-count` : undefined"
                    :keydown="editing ? undefined : commands.onKeydown"
                    :mentionsDisabled="!editing && commands.suggestions.value.length > 0"
                    @submit="submit()"
                    @cancel="cancelEdit"
                    @files="onEditorFiles"
                    @focus="onFocus"
                    @blur="onBlur" />

                <!-- Between the text box and the toolbar on a phone, so it sits above the
                     keyboard (design §3a); a popover above the text box from md. -->
                <ComposerCommandStrip
                    v-if="!editing"
                    :suggestions="commands.suggestions.value"
                    :active="commands.active.value"
                    :error="commands.error.value"
                    :listId="`${id}-commands`"
                    @pick="commands.pick" />
                <ComposerMentionLinks
                    v-if="!editor?.mentionsOpen"
                    :state="active"
                    :directory="directory" />
                <slot
                    name="strip"
                    :state="state" />
                <!-- The images (16c), above the toolbar so on a phone they stay above the
                     keyboard, with the caption nudge under them. A note being edited can
                     also reorder them, and says which of its own images Save deletes. -->
                <ImageAttachmentStrip
                    v-if="!slim"
                    :campaignId="campaignId"
                    :attachments="active.attachments"
                    :movable="!!editing"
                    @remove="(key) => activeUploads.remove(key)"
                    @retry="(key) => activeUploads.retry(key)"
                    @move="(key, delta) => activeUploads.move(key, delta)"
                    @missing="(key) => activeUploads.remove(key)" />
                <p
                    v-if="removedCount > 0"
                    class="px-1 text-xs text-muted-foreground">
                    {{ removedCount === 1 ? IMAGE_MESSAGES.willBeDeleted : `${removedCount} images will be deleted.` }}
                </p>
                <ImageCaptionNudge
                    v-if="nudging && !editing"
                    @postAnyway="post({ confirmed: true })"
                    @addCaption="addCaption" />
            </div>
            <input
                ref="fileInput"
                type="file"
                accept="image/*"
                multiple
                hidden
                @change="onFilesPicked" />
            <!-- 📷 (16e): the same, from the camera, on touch screens only. -->
            <input
                ref="cameraInput"
                type="file"
                accept="image/*"
                capture="environment"
                hidden
                @change="onFilesPicked" />

            <p
                v-if="active.text.length >= NOTE_TEXT_WARN_AT"
                :id="`${id}-count`"
                :class="['text-right text-xs', overLimit ? 'text-destructive-tint' : 'text-muted-foreground']">
                {{ storedLength.toLocaleString() }} / {{ NOTE_TEXT_MAX.toLocaleString() }}
            </p>

            <ComposerToolbar
                :items="toolbarItems"
                :mode="editing ? 'edit' : 'post'"
                :canPost="editing ? canSave : canPost"
                :waiting="editing ? saving || editUploads.busy.value : waiting"
                @cancel="cancelEdit" />
        </form>
        <ComposerRevealDialog
            ref="reveal"
            :campaignId="campaignId" />
    </div>
</template>

<script setup lang="ts">
    import { useInfiniteQuery, useQuery, useQueryClient } from "@tanstack/vue-query";
    import { useElementSize, useMediaQuery, useNow } from "@vueuse/core";
    import type { ComposerEditorApi } from "./Editor.vue";
    import { AtSign, Bold, CalendarPlus, Camera, ImagePlus, Italic, List, LoaderCircle, Pencil, ScrollText } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import type { Campaign, EntrySummary, SessionStreamFilter, Visibility } from "~/utils/api/types";
    import type { RevealItem } from "~/utils/mentions";
    import { currentMember } from "~/utils/campaign";
    import { aboutPrefill, relinkNewEntry, revealCheck, toStoredText } from "~/utils/mentions";
    import {
        NOTE_TEXT_MAX,
        NOTE_TEXT_WARN_AT,
        buildPostBody,
        canPostNote,
        gapPromptText,
        initialComposerState,
        loadDraft,
        newEntryName,
        newestNoteAt,
        nextSessionNumber,
        noSessionYet,
        noteWriteError,
        optimisticNote,
        resetAfterPost,
        saveDraft,
        showGapPrompt,
        targetSession,
        type ComposerState,
        type ComposerToolbarItem,
    } from "~/utils/composer";
    import {
        getSessionStreamQuery,
        getSessionsQuery,
        invalidateSessionStreams,
        postNoteMutation,
        putNoteMutation,
        startSessionMutation,
    } from "~/utils/queries/sessions";
    import { invalidateEntries, useEntryDirectory } from "~/utils/queries/entries";
    import { composerPinned } from "~/utils/keyboardInset";
    import { flattenSessions, newPendingNoteId } from "~/utils/sessionStreamCache";
    import {
        IMAGE_MESSAGES,
        captionNudge,
        failUploads,
        imageFiles,
        readyImages,
        removedNoteImages,
    } from "~/utils/images";
    import {
        canSaveNoteEdit,
        emptyNoteEditState,
        noteEditAudience,
        noteEditPlan,
        noteEditState,
        type NoteEditState,
    } from "~/utils/noteEdit";
    import {
        SHARE_MESSAGES,
        browserCaches,
        captionAfterShare,
        deleteShare,
        readShare,
        shareIsEmpty,
        shareWarnings,
    } from "~/utils/shareTarget";
    import { composeFits } from "~/utils/searchActions";

    const props = withDefaults(
        defineProps<{
            campaign: Campaign;
            /** The stream's filter, so the composer shares its query. */
            filter?: SessionStreamFilter;
            /** "Add a note about X" (15c, `?about=`): start the text with its mention. */
            about?: Pick<EntrySummary, "id" | "name" | "visibility">;
            /** A share from the phone's share sheet (16e, `?share=`): attach its images. */
            share?: string;
            /** "New note mentioning X" from ⌘K (17c, `?compose=`): the text to start with. */
            compose?: string;
            /**
             * The combat page's composer (design §3, 18e.5): the text, the visibility picker
             * and send, posting to the current session. No session picker, gap prompt or
             * images, and a draft of its own, so the Campaign tab's draft (and its images)
             * never turns up in a fight.
             */
            slim?: boolean;
        }>(),
        { filter: "All", about: undefined, share: undefined, compose: undefined, slim: false }
    );
    const emit = defineEmits<{ posted: []; aboutUsed: []; shareUsed: []; composeUsed: [] }>();

    const id = useId();
    const campaignId = computed(() => props.campaign.id);
    /** Where the draft is kept: the campaign's, or the slim composer's own. */
    const draftId = (id: string) => (props.slim ? `${id}:slim` : id);
    const storage = import.meta.client ? safeLocalStorage() : undefined;

    // ── State ────────────────────────────────────────────────────────────────
    // One reactive object: the pickers, the recap toggle and 14e's commands all
    // write to it.
    const state = reactive<ComposerState>(initialComposerState(loadDraft(storage, draftId(campaignId.value))));

    watch(campaignId, (next) => {
        uploads.release(state.attachments);
        Object.assign(state, initialComposerState(loadDraft(storage, draftId(next))));
    });
    // The draft keeps the mentions' links and new entries too (15d), and the uploaded
    // images (16c).
    watch(
        () => [state.text, state.links, state.newEntries, state.attachments] as const,
        () => {
            saveDraft(storage, draftId(campaignId.value), state);
        }
    );

    // ── "Add a note about X" (15c) ───────────────────────────────────────────
    // Consumed once: the text starts with the entry's mention, and a DM or Me entry
    // sets the note's visibility to match. The page then drops `?about=`.
    watch(
        () => props.about,
        (entry) => {
            if (!entry) return;
            const prefill = aboutPrefill(state, entry);
            state.text = prefill.text;
            state.links = prefill.links;
            if (prefill.visibility) state.visibility = prefill.visibility;
            emit("aboutUsed");
            void nextTick(() => editor.value?.focus("end"));
        },
        { immediate: true }
    );

    // ── Sessions and the gap prompt ─────────────────────────────────────────
    const sessionsQuery = useQuery(getSessionsQuery(campaignId));
    const streamQuery = useInfiniteQuery(getSessionStreamQuery(campaignId, () => props.filter));
    const streamSessions = computed(() => flattenSessions(streamQuery.data.value));

    // The picker lists every session (GET sessions). Until that answers, the loaded
    // stream stands in, so the current session is known for the optimistic note.
    const sessions = computed(
        () => sessionsQuery.data.value?.sessions ?? [...streamSessions.value.map((s) => s.session)].reverse()
    );
    const nextNumber = computed(() => nextSessionNumber(sessions.value));
    // A campaign has no session until a member starts Session 1, and a note needs one.
    // Blocking on this asks whether we *know* there is none, not whether we know of one
    // yet: the call to action in the composer's place (step 17) and the post guard both
    // read the same value, so they never disagree about it.
    const sessionsKnown = computed(() => ({
        hasSession: sessions.value.length > 0,
        sessionsLoaded: sessionsQuery.isSuccess.value,
    }));
    const noSession = computed(() => noSessionYet(sessionsKnown.value));

    // Re-evaluated every minute so an open tab notices the gap.
    const now = useNow({ interval: 60_000 });
    const newestAt = computed(() => newestNoteAt(streamSessions.value));
    const gapVisible = computed(() =>
        showGapPrompt(sessionsQuery.data.value?.suggestNextSession, newestAt.value, now.value)
    );
    const gapText = computed(() => gapPromptText(newestAt.value, nextNumber.value, now.value));

    const startSession = startSessionMutation();
    async function start() {
        try {
            const session = await startSession.mutateAsync({
                campaignId: campaignId.value,
                number: nextNumber.value,
            });
            // The new session is the current one; target it. From the call to action the
            // text box only renders on the next tick, once the list has the session.
            state.sessionId = session.isCurrent ? null : session.id;
            void nextTick(focus);
        } catch (error) {
            toast.error(
                apiErrorStatus(error) === 409
                    ? "Someone else started a session. Try again."
                    : apiErrorMessage(error, "Could not start the session.")
            );
        }
    }

    // ── Posting ──────────────────────────────────────────────────────────────
    const postNote = postNoteMutation();
    const queryClient = useQueryClient();
    // The limit counts the stored form (15d), which is what the API checks.
    // While editing (step 17) the count is the edit's.
    const storedLength = computed(() => toStoredText(active.value.text, active.value.links).trim().length);
    // With images the caption may be empty (16c). ➤ waits for uploads, and is off while
    // one has failed.
    const canPost = computed(() =>
        canPostNote({
            ...sessionsKnown.value,
            uploadsFailed: uploads.failed.value,
            text: state.text,
            attachmentCount: state.attachments.length,
            storedLength: storedLength.value,
        })
    );
    const overLimit = computed(() => storedLength.value > NOTE_TEXT_MAX);
    /** The session a post goes to: the picked one, or the current one (null) when slim. */
    const postSessionId = computed(() => (props.slim ? null : state.sessionId));
    const viewer = computed(() => ({
        memberId: props.campaign.currentMemberId,
        isDm: currentMember(props.campaign)?.role === "DM",
    }));
    const reveal = useTemplateRef<{ confirm: (v: Visibility, items: RevealItem[]) => Promise<boolean> }>("reveal");
    const placeholder = computed(() => {
        if (active.value.attachments.length > 0) return IMAGE_MESSAGES.captionPlaceholder;
        if (editing.value) return "Edit the note…";
        const target = targetSession(postSessionId.value, sessions.value);
        return target ? `Write a note in Session ${target.number}…` : "Write a session note…";
    });

    async function post({ confirmed = false }: { confirmed?: boolean } = {}) {
        if (uploads.failed.value) return;
        // No "no session yet" guard: with none the composer is only its call to action
        // (step 17), so nothing can post. canPostNote still refuses, for ➤.
        // ➤ while images are still going up: post once they are all up.
        if (uploads.busy.value) {
            waiting.value = true;
            return;
        }
        waiting.value = false;
        // Images and no caption: ask first (§3, §5).
        if (captionNudge({ text: state.text, imageCount: state.attachments.length, confirmed }) === "confirm") {
            nudging.value = true;
            return;
        }
        nudging.value = false;
        const body = buildPostBody(props.slim ? { ...state, sessionId: null } : state, sessions.value);
        if (!body) return;
        if (!(await confirmReveal(body.visibility, body.text))) {
            focus();
            return;
        }
        const target = targetSession(postSessionId.value, sessions.value);
        const optimistic = target
            ? optimisticNote({
                  tempId: newPendingNoteId(),
                  body,
                  session: target,
                  authorMemberId: props.campaign.currentMemberId,
                  now: new Date(),
                  images: readyImages(state.attachments),
              })
            : undefined;

        // Optimistic: clear at once; the note shows under a temporary id.
        const sent: ComposerState = { ...state };
        Object.assign(state, resetAfterPost());
        focus();
        try {
            await postNote.mutateAsync({ campaignId: campaignId.value, body, optimistic });
            // The images are on the note now: only their previews go.
            uploads.release(sent.attachments);
            emit("posted");
        } catch (error) {
            const failure = noteWriteError(error);
            // A retry after a timeout: the first try went through, note and entries.
            if (failure.kind === "alreadySaved") {
                toast.info("That note was already posted.");
                refreshAfterRetry();
                return;
            }
            // Give the text and images back unless something new was written or attached
            // meanwhile. The images are still uploaded, so a retry is quick.
            const restore = state.text.trim() === "" && state.attachments.length === 0;
            if (restore) Object.assign(state, sent);
            else uploads.release(sent.attachments);
            if (failure.kind === "images") {
                if (restore) state.attachments = failUploads(state.attachments, failure.message);
                toast.error(failure.message);
                return;
            }
            if (failure.kind === "duplicate") {
                // Someone made an entry with that name meanwhile: link it instead.
                const name = newEntryName(sent.newEntries, failure.newEntryId);
                if (restore) Object.assign(state, relinkNewEntry(state, failure.newEntryId, failure.existingEntryId));
                toast.error(
                    restore
                        ? `"${name}" already exists, so the mention now links to it. Post again.`
                        : `"${name}" already exists. Nothing was posted.`
                );
                return;
            }
            toast.error(apiErrorMessage(error, "Could not post the note."));
        }
    }

    /** The reveal warning (15d), for a post and an edit: nothing is revealed without the tap. */
    async function confirmReveal(audience: Visibility, storedText: string): Promise<boolean> {
        const warn = revealCheck(audience, storedText, directory.value, viewer.value);
        return warn.length === 0 || !!(await reveal.value?.confirm(audience, warn));
    }

    /** After a retry that had already gone through: fetch the note and its entries. */
    function refreshAfterRetry() {
        void invalidateSessionStreams(queryClient, campaignId.value);
        void invalidateEntries(queryClient, campaignId.value);
    }

    /** ➤, Enter and Mod+Enter: post, or save the note being edited (step 17). */
    function submit() {
        if (editing.value) void save();
        else void post();
    }

    // ── The toolbar ──────────────────────────────────────────────────────────
    // Enter, Mod+Enter and the shortcuts are the text box's own (step 17, the rich text box).
    const touch = useMediaQuery("(pointer: coarse)");
    const editor = useTemplateRef<ComposerEditorApi>("editor");
    const isMac = import.meta.client && /Mac|iPhone|iPad/.test(navigator.platform);
    const mod = isMac ? "⌘" : "Ctrl+";

    const toolbarItems = computed<ComposerToolbarItem[]>(() => [
        // First (design §3a): for keyboards where `@` is hard to reach.
        { id: "mention", label: "Mention an entry", icon: AtSign, shortcut: `${mod}K`, run: () => editor.value?.triggerMention() },
        // 🖼 (16c), on every screen size, and 📷 (16e), on touch screens only: on desktop
        // `capture` is ignored, and it would be a second 🖼. The slim composer has neither.
        ...(props.slim
            ? []
            : [
                  { id: "gallery", label: "Attach images", icon: ImagePlus, run: pickImages } satisfies ComposerToolbarItem,
                  ...(touch.value
                      ? [{ id: "camera", label: "Take a photo", icon: Camera, run: takePhoto } satisfies ComposerToolbarItem]
                      : []),
              ]),
        {
            id: "bold",
            label: "Bold",
            icon: Bold,
            shortcut: `${mod}B`,
            pressed: editor.value?.active.bold ?? false,
            run: () => editor.value?.toggle("bold"),
        },
        {
            id: "italic",
            label: "Italic",
            icon: Italic,
            shortcut: `${mod}I`,
            pressed: editor.value?.active.italic ?? false,
            run: () => editor.value?.toggle("italic"),
        },
        {
            id: "list",
            label: "List",
            icon: List,
            shortcut: isMac ? "⌘⇧8" : "Ctrl+Shift+8",
            pressed: editor.value?.active.bulletList ?? false,
            run: () => editor.value?.toggle("bulletList"),
        },
        {
            id: "recap",
            label: "Recap",
            text: "Recap",
            icon: ScrollText,
            pressed: active.value.isRecap,
            separatorBefore: true,
            run: () => (active.value.isRecap = !active.value.isRecap),
        },
    ]);

    // ── Images (16c) ─────────────────────────────────────────────────────────
    const uploads = useImageAttachments({ attachments: toRef(state, "attachments"), campaignId });
    const waiting = ref(false);
    const nudging = ref(false);
    const dragging = ref(false);
    const fileInput = useTemplateRef<HTMLInputElement>("fileInput");
    const cameraInput = useTemplateRef<HTMLInputElement>("cameraInput");

    // Once every upload is done, a waiting ➤ posts (or nudges); a failure stops it.
    watch(uploads.busy, (busy) => {
        if (busy || !waiting.value) return;
        waiting.value = false;
        if (!uploads.failed.value) void post();
    });
    // A caption, or no images any more, answers the nudge.
    watch(
        () => [state.text.trim() !== "", state.attachments.length] as const,
        ([hasCaption, count]) => {
            if (hasCaption || count === 0) nudging.value = false;
            if (count === 0) waiting.value = false;
        }
    );

    function pickImages() {
        fileInput.value?.click();
    }
    function takePhoto() {
        cameraInput.value?.click();
    }
    /** Attaches files; paste and drop pass only images, and say so when some were not. */
    function attach(files: readonly File[], { onlyImages }: { onlyImages: boolean }) {
        const images = onlyImages ? imageFiles(files) : [...files];
        if (onlyImages && images.length < files.length) toast.error(IMAGE_MESSAGES.unsupported);
        activeUploads.value.add(images);
    }
    function onFilesPicked(event: Event) {
        const input = event.target as HTMLInputElement;
        attach(Array.from(input.files ?? []), { onlyImages: false });
        input.value = "";
        // iOS: the picker blurred the text box; focus brings the pinned composer back.
        focus();
    }
    const carriesFiles = (event: DragEvent) =>
        !props.slim && !!event.dataTransfer && Array.from(event.dataTransfer.types).includes("Files");
    /** Files pasted into the text box. The slim composer takes no images. */
    function onEditorFiles(files: File[]) {
        if (props.slim) toast.info("Images can be posted from the Campaign tab.");
        else activeUploads.value.add(files);
    }
    function onDragOver(event: DragEvent) {
        if (!carriesFiles(event)) return;
        event.preventDefault();
        dragging.value = true;
    }
    function onDragLeave(event: DragEvent) {
        // Moving between the composer's own children is not leaving it.
        if (event.relatedTarget instanceof Node && form.value?.contains(event.relatedTarget)) return;
        dragging.value = false;
    }
    function onDrop(event: DragEvent) {
        dragging.value = false;
        if (!carriesFiles(event)) return;
        event.preventDefault();
        attach(Array.from(event.dataTransfer?.files ?? []), { onlyImages: true });
    }
    function addCaption() {
        nudging.value = false;
        focus();
    }

    // ── Editing a note (step 17) ────────────────────────────────────────────
    // The card's "Edit" (or a loose end's, 19e) brings the note up here. The
    // edit has its own state and upload queue beside the draft's (`utils/noteEdit.ts`),
    // so the draft, its saved copy and its uploads carry on untouched, and it is simply
    // back when the edit ends. `active` is whichever the text box, the images and the
    // recap toggle are showing.
    const edit = useComposerEdit(campaignId.value);
    const editing = edit.target;
    const editState = reactive<NoteEditState>(emptyNoteEditState());
    const editUploads = useImageAttachments({ attachments: toRef(editState, "attachments"), campaignId });
    const active = computed<NoteEditState>(() => (editing.value ? editState : state));
    const activeUploads = computed(() => (editing.value ? editUploads : uploads));
    // The note's own images that Save deletes.
    const removedCount = computed(() =>
        editing.value ? removedNoteImages(editing.value.note.images, editState.attachments) : 0
    );

    const putNote = putNoteMutation();
    const saving = computed(() => putNote.isPending.value);
    const canSave = computed(() => canSaveNoteEdit(editState) && !saving.value);
    watch(saving, (value) => (edit.locked.value = value));

    // Starting, switching and ending an edit. Leaving one drops the uploads its note
    // never took (after a save there are none left: they are on the note).
    watch(
        editing,
        (next, previous) => {
            if (previous) editUploads.discard();
            if (!next) {
                Object.assign(editState, emptyNoteEditState());
                // Back to the draft; on a phone the keyboard stays down.
                if (previous && !touch.value) void nextTick(focus);
                return;
            }
            Object.assign(editState, noteEditState(next.note));
            // A ➤ waiting for the draft's uploads must not post in the middle of an edit.
            waiting.value = false;
            nudging.value = false;
            void nextTick(() => {
                editor.value?.focus("end");
                // The note stays in view above the composer (and the keyboard, which
                // takes a moment to come up on a phone).
                setTimeout(() => {
                    document.getElementById(`note-${next.note.id}`)?.scrollIntoView({ block: "nearest", behavior: "smooth" });
                }, 300);
            });
        },
        { immediate: true }
    );

    /** Cancel and Esc: back to the draft, with no request. */
    function cancelEdit() {
        if (editing.value && !saving.value) edit.end();
    }

    async function save() {
        const target = editing.value;
        if (!target || saving.value) return;
        const plan = noteEditPlan(target.note, editState);
        if (plan.kind === "blocked") return;
        // Nothing changed: no request, the edit just ends.
        if (plan.kind === "unchanged") {
            edit.end();
            return;
        }
        if (!(await confirmReveal(noteEditAudience(target.note), plan.body.text))) {
            editor.value?.focus();
            return;
        }
        try {
            await putNote.mutateAsync({ campaignId: campaignId.value, noteId: target.note.id, ...plan.body });
            savedEdit();
        } catch (error) {
            const failure = noteWriteError(error);
            switch (failure.kind) {
                case "alreadySaved":
                    // A retry after a timeout: the first save went through.
                    toast.info("That edit was already saved.");
                    refreshAfterRetry();
                    savedEdit();
                    return;
                case "duplicate": {
                    // Someone made an entry with that name meanwhile: link it instead.
                    const name = newEntryName(editState.newEntries, failure.newEntryId);
                    Object.assign(editState, relinkNewEntry(editState, failure.newEntryId, failure.existingEntryId));
                    toast.error(`"${name}" already exists, so the mention now links to it. Save again.`);
                    return;
                }
                case "images":
                    editState.attachments = failUploads(editState.attachments, failure.message);
                    toast.error(failure.message);
                    return;
                default:
                    toast.error(apiErrorMessage(error, "Could not save the note."));
            }
        }
    }

    /** Saved: the new images are on the note now, so only their previews go. */
    function savedEdit() {
        editUploads.release(editState.attachments);
        editState.attachments = [];
        edit.end();
    }

    // The composer going away (another campaign, another page) ends the edit.
    onBeforeUnmount(() => {
        edit.locked.value = false;
        if (!editing.value) return;
        editUploads.discard();
        edit.end();
    });

    // ── "New note mentioning X" (17c) ───────────────────────────────────────
    // Consumed once, as `?about=` is. An empty composer takes the text (`@Klarg`) with
    // the caret at its end, so the `@` picker opens on it and offers the matches or
    // Create. A draft, or a note being edited, is never overwritten: the text is dropped.
    watch(
        () => props.compose,
        (text) => {
            if (text === undefined) return;
            emit("composeUsed");
            const fits = composeFits({
                text: state.text,
                attachmentCount: state.attachments.length,
                editing: !!editing.value,
            });
            if (!fits) {
                toast.info(editing.value ? "Finish editing the note first." : "Your draft was kept.");
                return;
            }
            state.text = text;
            state.links = {};
            // The text box writes the text in first; the caret then lands after the
            // query, and TipTap's suggestion plugin opens the picker on that selection.
            void nextTick(() => editor.value?.focus("end"));
        },
        { immediate: true }
    );

    // ── A share (16e) ────────────────────────────────────────────────────────
    // Consumed once, as `?about=` is: the service worker kept the shared images and
    // text in Cache Storage. They are attached (prepared and uploaded as usual), the
    // text fills an empty caption, and the note targets the current session. Then the
    // cache entry goes and the page drops `?share=`.
    const takenShares = new Set<string>();
    watch(
        () => props.share,
        async (shareId) => {
            if (!shareId || takenShares.has(shareId)) return;
            takenShares.add(shareId);
            const caches = browserCaches();
            const item = await readShare(caches, shareId).catch(() => undefined);
            await deleteShare(caches, shareId).catch(() => undefined);
            emit("shareUsed");
            if (!item || shareIsEmpty(item)) {
                toast.error(item && item.notImages > 0 ? SHARE_MESSAGES.notImages : SHARE_MESSAGES.missing);
                return;
            }
            state.sessionId = null;
            state.text = captionAfterShare(state.text, item);
            uploads.add(item.files);
            for (const warning of shareWarnings(item)) toast.warning(warning);
            void nextTick(focus);
        },
        { immediate: true }
    );

    // ── Commands (14e) ───────────────────────────────────────────────────────
    const commands = useComposerCommands({
        state,
        sessions,
        caret: {
            get: () => editor.value?.caretOffset() ?? null,
            set: (offset) => editor.value?.setCaretOffset(offset),
            focus: () => focus(),
            hasFocus: () => editor.value?.hasFocus() ?? false,
        },
    });

    // ── Mentions (15d) ───────────────────────────────────────────────────────
    // The text box runs the `@` picker; the "Links" row and the reveal warning read
    // the same entry directory.
    const directory = useEntryDirectory(campaignId);

    // ── Pinned above the keyboard (14e) ──────────────────────────────────────
    const phone = useMediaQuery("(max-width: 767.98px)");
    const inset = useKeyboardInset();
    const focused = ref(false);
    // A short grace period, so focus moving to a toolbar button and back does not
    // unpin and re-pin the composer.
    let blurTimer: ReturnType<typeof setTimeout> | undefined;
    function onFocus() {
        clearTimeout(blurTimer);
        focused.value = true;
    }
    function onBlur() {
        clearTimeout(blurTimer);
        blurTimer = setTimeout(() => (focused.value = false), 120);
    }
    const pinned = computed(() => composerPinned({ focused: focused.value, phone: phone.value, inset: inset.value }));
    const form = useTemplateRef<HTMLFormElement>("form");
    const { height: formHeight } = useElementSize(form, undefined, { box: "border-box" });
    // No transition on `bottom`: iOS moves the keyboard in steps and a transition lags
    // behind it. With no keyboard up, the home indicator's safe area is kept clear.
    const pinnedStyle = computed(() => ({
        bottom: `${inset.value}px`,
        paddingBottom: inset.value > 0 ? undefined : "calc(0.5rem + env(safe-area-inset-bottom))",
    }));
    const pinnedState = useComposerPinned();
    watch(pinned, (value) => (pinnedState.value = value), { immediate: true });
    onBeforeUnmount(() => {
        clearTimeout(blurTimer);
        pinnedState.value = false;
    });

    function focus() {
        editor.value?.focus();
    }

    function safeLocalStorage(): Storage | undefined {
        try {
            return window.localStorage;
        } catch {
            return undefined;
        }
    }

    // `attach` takes files the way 🖼 does (📷 and the share target go through it too).
    defineExpose({ focus, state, attach: (files: readonly File[]) => activeUploads.value.add(files) });
</script>
