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
            @submit.prevent="post()"
            @dragover="onDragOver"
            @dragleave="onDragLeave"
            @drop="onDrop">
            <!-- Dropping files on the composer attaches them (16c, desktop). -->
            <div
                v-if="dragging"
                class="pointer-events-none absolute inset-0 z-10 flex items-center justify-center rounded-md border-2 border-dashed border-gold bg-background/90 text-sm font-medium text-gold">
                Drop images to attach them
            </div>
            <div class="flex min-w-0 items-center gap-1">
                <ComposerSessionPicker
                    v-model="state.sessionId"
                    :sessions="sessions"
                    :loaded="sessionsQuery.isSuccess.value"
                    :starting="startSession.isPending.value"
                    @start="start" />
                <ComposerVisibilityPicker v-model="state.visibility" />
            </div>

            <ComposerGapPrompt
                v-if="gapVisible"
                :text="gapText"
                :nextNumber="nextNumber"
                :starting="startSession.isPending.value"
                @accept="start" />

            <div class="relative flex flex-col gap-1">
                <!-- The text box (step 17, the rich text box): the `@` suggestions come with it, as the
                     mention strip under it on a phone (above the keyboard, where the `/`
                     strip goes) and a popover at the caret from md. The `/` strip wins
                     while it is open, so the two are never shown together. -->
                <ComposerEditor
                    ref="editor"
                    v-model="state.text"
                    v-model:links="state.links"
                    v-model:newEntries="state.newEntries"
                    :campaignId="campaignId"
                    :placeholder="placeholder"
                    label="Session note"
                    :describedBy="overLimit ? `${id}-count` : undefined"
                    :keydown="commands.onKeydown"
                    :mentionsDisabled="commands.suggestions.value.length > 0"
                    @submit="post()"
                    @files="uploads.add"
                    @focus="onFocus"
                    @blur="onBlur" />

                <!-- Between the text box and the toolbar on a phone, so it sits above the
                     keyboard (design §3a); a popover above the text box from md. -->
                <ComposerCommandStrip
                    :suggestions="commands.suggestions.value"
                    :active="commands.active.value"
                    :error="commands.error.value"
                    :listId="`${id}-commands`"
                    @pick="commands.pick" />
                <ComposerMentionLinks
                    v-if="!editor?.mentionsOpen"
                    :state="state"
                    :directory="directory" />
                <slot
                    name="strip"
                    :state="state" />
                <!-- The images (16c), above the toolbar so on a phone they stay above the
                     keyboard, with the caption nudge under them. -->
                <ImageAttachmentStrip
                    :campaignId="campaignId"
                    :attachments="state.attachments"
                    @remove="uploads.remove"
                    @retry="uploads.retry"
                    @missing="uploads.remove" />
                <ImageCaptionNudge
                    v-if="nudging"
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
                v-if="state.text.length >= NOTE_TEXT_WARN_AT"
                :id="`${id}-count`"
                :class="['text-right text-xs', overLimit ? 'text-destructive-tint' : 'text-muted-foreground']">
                {{ storedLength.toLocaleString() }} / {{ NOTE_TEXT_MAX.toLocaleString() }}
            </p>

            <ComposerToolbar
                :items="toolbarItems"
                :canPost="canPost"
                :waiting="waiting" />
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
    import { AtSign, Bold, CalendarPlus, Camera, ImagePlus, Italic, List, LoaderCircle, ScrollText } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import type { Campaign, EntrySummary, SessionStreamFilter, Visibility } from "~/utils/api/types";
    import type { RevealItem } from "~/utils/mentions";
    import { currentMember } from "~/utils/campaign";
    import {
        aboutPrefill,
        newEntryErrorFrom,
        relinkNewEntry,
        revealCheck,
        toStoredText,
    } from "~/utils/mentions";
    import {
        NOTE_TEXT_MAX,
        NOTE_TEXT_WARN_AT,
        buildPostBody,
        canPostNote,
        gapPromptText,
        initialComposerState,
        loadDraft,
        newestNoteAt,
        nextSessionNumber,
        noSessionYet,
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
        imageIdsErrorFrom,
        readyImages,
    } from "~/utils/images";
    import {
        SHARE_MESSAGES,
        browserCaches,
        captionAfterShare,
        deleteShare,
        readShare,
        shareIsEmpty,
        shareWarnings,
    } from "~/utils/shareTarget";

    const props = withDefaults(
        defineProps<{
            campaign: Campaign;
            /** The stream's filter, so the composer shares its query. */
            filter?: SessionStreamFilter;
            /** "Add a note about X" (15c, `?about=`): start the text with its mention. */
            about?: Pick<EntrySummary, "id" | "name" | "visibility">;
            /** A share from the phone's share sheet (16e, `?share=`): attach its images. */
            share?: string;
        }>(),
        { filter: "All", about: undefined, share: undefined }
    );
    const emit = defineEmits<{ posted: []; aboutUsed: []; shareUsed: [] }>();

    const id = useId();
    const campaignId = computed(() => props.campaign.id);
    const storage = import.meta.client ? safeLocalStorage() : undefined;

    // ── State ────────────────────────────────────────────────────────────────
    // One reactive object: the pickers, the recap toggle and 14e's commands all
    // write to it.
    const state = reactive<ComposerState>(initialComposerState(loadDraft(storage, campaignId.value)));

    watch(campaignId, (next) => {
        uploads.release(state.attachments);
        Object.assign(state, initialComposerState(loadDraft(storage, next)));
    });
    // The draft keeps the mentions' links and new entries too (15d), and the uploaded
    // images (16c).
    watch(
        () => [state.text, state.links, state.newEntries, state.attachments] as const,
        () => {
            saveDraft(storage, campaignId.value, state);
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
    const storedLength = computed(() => toStoredText(state.text, state.links).trim().length);
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
    const viewer = computed(() => ({
        memberId: props.campaign.currentMemberId,
        isDm: currentMember(props.campaign)?.role === "DM",
    }));
    const reveal = useTemplateRef<{ confirm: (v: Visibility, items: RevealItem[]) => Promise<boolean> }>("reveal");
    const placeholder = computed(() => {
        if (state.attachments.length > 0) return IMAGE_MESSAGES.captionPlaceholder;
        const target = targetSession(state.sessionId, sessions.value);
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
        const body = buildPostBody(state, sessions.value);
        if (!body) return;
        // The reveal warning (15d): nothing is revealed without the tap.
        const warn = revealCheck(body.visibility, body.text, directory.value, viewer.value);
        if (warn.length > 0 && !(await reveal.value?.confirm(body.visibility, warn))) {
            focus();
            return;
        }
        const target = targetSession(state.sessionId, sessions.value);
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
            const entryError = newEntryErrorFrom(error);
            // A retry after a timeout: the first try went through, note and entries.
            if (entryError?.kind === "alreadyCreated") {
                toast.info("That note was already posted.");
                void invalidateSessionStreams(queryClient, campaignId.value);
                void invalidateEntries(queryClient, campaignId.value);
                return;
            }
            // Give the text and images back unless something new was written or attached
            // meanwhile. The images are still uploaded, so a retry is quick.
            const restore = state.text.trim() === "" && state.attachments.length === 0;
            if (restore) Object.assign(state, sent);
            else uploads.release(sent.attachments);
            // 16b's `errors.imageIds`: an upload was swept or taken meanwhile; upload again.
            const imagesError = imageIdsErrorFrom(error);
            if (imagesError) {
                if (restore) state.attachments = failUploads(state.attachments, imagesError);
                toast.error(imagesError);
                return;
            }
            if (entryError?.kind === "duplicate") {
                // Someone made an entry with that name meanwhile: link it instead.
                const name = sent.newEntries.find((e) => e.id.toLowerCase() === entryError.newEntryId.toLowerCase())?.name;
                if (restore) Object.assign(state, relinkNewEntry(state, entryError.newEntryId, entryError.existingEntryId));
                toast.error(
                    restore
                        ? `"${name ?? "That entry"}" already exists, so the mention now links to it. Post again.`
                        : `"${name ?? "That entry"}" already exists. Nothing was posted.`
                );
                return;
            }
            toast.error(apiErrorMessage(error, "Could not post the note."));
        }
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
        // 🖼 (16c), on every screen size.
        { id: "gallery", label: "Attach images", icon: ImagePlus, run: pickImages },
        // 📷 (16e), on touch screens only: on desktop `capture` is ignored, and it
        // would be a second 🖼.
        ...(touch.value
            ? [{ id: "camera", label: "Take a photo", icon: Camera, run: takePhoto } satisfies ComposerToolbarItem]
            : []),
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
            pressed: state.isRecap,
            separatorBefore: true,
            run: () => (state.isRecap = !state.isRecap),
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
        uploads.add(images);
    }
    function onFilesPicked(event: Event) {
        const input = event.target as HTMLInputElement;
        attach(Array.from(input.files ?? []), { onlyImages: false });
        input.value = "";
        // iOS: the picker blurred the text box; focus brings the pinned composer back.
        focus();
    }
    const carriesFiles = (event: DragEvent) => !!event.dataTransfer && Array.from(event.dataTransfer.types).includes("Files");
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
    defineExpose({ focus, state, attach: (files: readonly File[]) => uploads.add(files) });
</script>
