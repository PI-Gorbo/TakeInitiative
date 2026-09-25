<template>
    <!-- The composer (glossary §1, design §3). It sits below the stream, outside its
         scroller. While its text box has focus on a phone it is pinned directly above
         the on-screen keyboard (design §3a, invariant 11), and this wrapper keeps its
         place, plus the keyboard's height, so the stream ends where the composer starts. -->
    <div
        class="shrink-0"
        :style="pinned ? { height: `${formHeight + inset}px` } : undefined">
        <form
            ref="form"
            :class="[
                'flex flex-col gap-1 border-t bg-background px-2 pb-2 pt-1',
                pinned && 'fixed inset-x-0 z-30 px-safe',
            ]"
            :style="pinned ? pinnedStyle : undefined"
            aria-label="Composer"
            @submit.prevent="post">
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
                <textarea
                    ref="textarea"
                    v-model="state.text"
                    rows="1"
                    :enterkeyhint="touch ? 'enter' : 'send'"
                    :placeholder="placeholder"
                    aria-label="Session note"
                    :aria-describedby="overLimit ? `${id}-count` : undefined"
                    class="max-h-[40dvh] min-h-11 w-full resize-none overflow-y-auto rounded-md border bg-background px-3 py-2.5 text-base leading-snug outline-none placeholder:text-muted-foreground focus-visible:ring-1 focus-visible:ring-ring md:min-h-10 md:py-2 md:text-sm"
                    @keydown="onKeydown"
                    @input="grow"
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
                <!-- The strip slot: step 15's `@` suggestions go in the same place. -->
                <slot
                    name="strip"
                    :state="state" />
            </div>

            <p
                v-if="state.text.length >= NOTE_TEXT_WARN_AT"
                :id="`${id}-count`"
                :class="['text-right text-xs', overLimit ? 'text-destructive-tint' : 'text-muted-foreground']">
                {{ state.text.trim().length.toLocaleString() }} / {{ NOTE_TEXT_MAX.toLocaleString() }}
            </p>

            <ComposerToolbar
                :items="toolbarItems"
                :canPost="canPost" />
        </form>
    </div>
</template>

<script setup lang="ts">
    import { useInfiniteQuery, useQuery } from "@tanstack/vue-query";
    import { useElementSize, useMediaQuery, useNow } from "@vueuse/core";
    import { Bold, Italic, List, ScrollText } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import type { Campaign, SessionStreamFilter } from "~/utils/api/types";
    import {
        NOTE_TEXT_MAX,
        NOTE_TEXT_WARN_AT,
        buildPostBody,
        enterAction,
        gapPromptText,
        initialComposerState,
        loadDraft,
        newestNoteAt,
        nextSessionNumber,
        optimisticNote,
        postableText,
        resetAfterPost,
        saveDraft,
        showGapPrompt,
        targetSession,
        toggleInline,
        toggleList,
        type ComposerState,
        type ComposerToolbarItem,
        type InlineFormat,
        type TextEdit,
    } from "~/utils/composer";
    import {
        getSessionStreamQuery,
        getSessionsQuery,
        postNoteMutation,
        startSessionMutation,
    } from "~/utils/queries/sessions";
    import { composerPinned } from "~/utils/keyboardInset";
    import { flattenSessions, newPendingNoteId } from "~/utils/sessionStreamCache";

    const props = withDefaults(
        defineProps<{
            campaign: Campaign;
            /** The stream's filter, so the composer shares its query. */
            filter?: SessionStreamFilter;
        }>(),
        { filter: "All" }
    );
    const emit = defineEmits<{ posted: [] }>();

    const id = useId();
    const campaignId = computed(() => props.campaign.id);
    const storage = import.meta.client ? safeLocalStorage() : undefined;

    // ── State ────────────────────────────────────────────────────────────────
    // One reactive object: the pickers, the recap toggle and 14e's commands all
    // write to it.
    const state = reactive<ComposerState>(initialComposerState(loadDraft(storage, campaignId.value)));

    watch(campaignId, (next) => Object.assign(state, initialComposerState(loadDraft(storage, next))));
    watch(
        () => state.text,
        (text) => {
            saveDraft(storage, campaignId.value, text);
            void nextTick(grow);
        }
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
            // The new session is the current one; target it.
            state.sessionId = session.isCurrent ? null : session.id;
            focus();
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
    const canPost = computed(() => postableText(state.text) !== null);
    const overLimit = computed(() => state.text.trim().length > NOTE_TEXT_MAX);
    const placeholder = computed(() => {
        const target = targetSession(state.sessionId, sessions.value);
        return target ? `Write a note in Session ${target.number}…` : "Write a session note…";
    });

    async function post() {
        const body = buildPostBody(state, sessions.value);
        if (!body) return;
        const target = targetSession(state.sessionId, sessions.value);
        const optimistic = target
            ? optimisticNote({
                  tempId: newPendingNoteId(),
                  body,
                  session: target,
                  authorMemberId: props.campaign.currentMemberId,
                  now: new Date(),
              })
            : undefined;

        // Optimistic: clear at once; the note shows under a temporary id.
        const sent: ComposerState = { ...state };
        Object.assign(state, resetAfterPost());
        focus();
        try {
            await postNote.mutateAsync({ campaignId: campaignId.value, body, optimistic });
            emit("posted");
        } catch (error) {
            // Give the text back unless something new was typed meanwhile.
            if (state.text.trim() === "") Object.assign(state, sent);
            toast.error(apiErrorMessage(error, "Could not post the note."));
        }
    }

    // ── Keys and the toolbar ─────────────────────────────────────────────────
    const touch = useMediaQuery("(pointer: coarse)");
    const textarea = useTemplateRef<HTMLTextAreaElement>("textarea");
    const isMac = import.meta.client && /Mac|iPhone|iPad/.test(navigator.platform);
    const mod = isMac ? "⌘" : "Ctrl+";

    function onKeydown(event: KeyboardEvent) {
        // The `/` strip takes the arrows, Tab, Enter and Esc while it is open.
        if (commands.onKeydown(event)) return;
        const action = enterAction(event, touch.value);
        if (action === "post") {
            event.preventDefault();
            void post();
            return;
        }
        if ((event.metaKey || event.ctrlKey) && !event.altKey && !event.shiftKey) {
            const key = event.key.toLowerCase();
            if (key === "b" || key === "i") {
                event.preventDefault();
                format(key === "b" ? "bold" : "italic");
            }
        }
    }

    function applyEdit(edit: (current: TextEdit) => TextEdit) {
        const el = textarea.value;
        const current: TextEdit = {
            text: state.text,
            selectionStart: el?.selectionStart ?? state.text.length,
            selectionEnd: el?.selectionEnd ?? state.text.length,
        };
        const next = edit(current);
        state.text = next.text;
        void nextTick(() => {
            el?.focus();
            el?.setSelectionRange(next.selectionStart, next.selectionEnd);
        });
    }
    const format = (kind: InlineFormat) => applyEdit((e) => toggleInline(e, kind));

    const toolbarItems = computed<ComposerToolbarItem[]>(() => [
        { id: "bold", label: "Bold", icon: Bold, shortcut: `${mod}B`, run: () => format("bold") },
        { id: "italic", label: "Italic", icon: Italic, shortcut: `${mod}I`, run: () => format("italic") },
        { id: "list", label: "List", icon: List, run: () => applyEdit(toggleList) },
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

    // ── Commands (14e) ───────────────────────────────────────────────────────
    const commands = useComposerCommands({ state, sessions, textarea });

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

    // ── The text box ─────────────────────────────────────────────────────────
    // Grows with its content up to 40% of the screen, then scrolls.
    function grow() {
        const el = textarea.value;
        if (!el) return;
        el.style.height = "auto";
        el.style.height = `${el.scrollHeight + (el.offsetHeight - el.clientHeight)}px`;
    }
    onMounted(grow);

    function focus() {
        textarea.value?.focus();
    }

    function safeLocalStorage(): Storage | undefined {
        try {
            return window.localStorage;
        } catch {
            return undefined;
        }
    }

    defineExpose({ focus, state });
</script>
