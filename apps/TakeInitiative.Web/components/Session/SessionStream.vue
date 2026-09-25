<template>
    <div class="relative flex min-h-0 flex-1 flex-col">
        <div
            ref="scroller"
            class="min-h-0 flex-1 overflow-y-auto overscroll-contain"
            aria-label="Session stream"
            role="feed"
            :aria-busy="streamQuery.isFetching.value"
            @scroll.passive="onScroll">
            <div
                ref="content"
                class="flex min-h-full flex-col justify-end pb-3">
                <LoadingFallback
                    v-if="!streamQuery.data.value"
                    :isLoading="streamQuery.isLoading.value"
                    :isError="streamQuery.isError.value"
                    iconSize="2x"
                    class="pt-8" />
                <template v-else>
                    <!-- The top: older sessions load when scrolled here. -->
                    <div class="flex min-h-11 items-center justify-center px-4 py-2 text-xs text-muted-foreground">
                        <span
                            v-if="streamQuery.isFetchingNextPage.value"
                            class="flex items-center gap-2">
                            <LoaderCircle
                                class="size-4 animate-spin"
                                aria-hidden="true" />
                            Loading older sessions…
                        </span>
                        <Button
                            v-else-if="streamQuery.hasNextPage.value"
                            variant="ghost"
                            class="h-11 text-xs text-muted-foreground"
                            @click="loadOlder">
                            Load older sessions
                        </Button>
                        <span v-else>The start of the campaign</span>
                    </div>

                    <section
                        v-for="entry in visibleSessions"
                        :key="entry.session.id"
                        :aria-label="`Session ${entry.session.number}`">
                        <SessionDivider
                            :campaignId="campaignId"
                            :session="entry.session"
                            :canEditTitle="isDm" />
                        <!-- Recaps sit directly under their divider. -->
                        <SessionNoteCard
                            v-for="note in entry.recaps"
                            :key="note.id"
                            :campaignId="campaignId"
                            :note="note"
                            :session="entry.session"
                            :authorName="authorName(note.authorMemberId)"
                            :currentMemberId="campaign.currentMemberId"
                            :isDm="isDm"
                            :highlighted="note.id === highlightedId"
                            @openActions="openSheet(note, $event)"
                            @openImage="imageViewer.open" />
                        <SessionNoteCard
                            v-for="note in entry.notes"
                            :key="note.id"
                            :campaignId="campaignId"
                            :note="note"
                            :session="entry.session"
                            :authorName="authorName(note.authorMemberId)"
                            :currentMemberId="campaign.currentMemberId"
                            :isDm="isDm"
                            :highlighted="note.id === highlightedId"
                            @openActions="openSheet(note, $event)"
                            @openImage="imageViewer.open" />
                    </section>

                    <div
                        v-if="noteCount === 0"
                        class="flex flex-col gap-1 px-4 py-6 text-center text-sm text-muted-foreground">
                        <p>{{ emptyState.title }}</p>
                        <p
                            v-if="emptyState.detail"
                            class="text-xs">
                            {{ emptyState.detail }}
                        </p>
                    </div>
                </template>
            </div>
        </div>

        <!-- New notes from others while scrolled up: a pill instead of a jump. -->
        <Transition name="fade">
            <button
                v-if="hasUnseen"
                type="button"
                class="absolute bottom-3 left-1/2 flex h-11 -translate-x-1/2 items-center gap-1 rounded-full bg-primary px-4 text-sm font-medium text-primary-foreground shadow-lg"
                @click="scrollToBottom('smooth')">
                New notes
                <ArrowDown
                    class="size-4"
                    aria-hidden="true" />
            </button>
        </Transition>

        <!-- Desktop: "Add to wiki" by a selection inside a note (15f). -->
        <WikiPromoteSelection
            :campaignId="campaignId"
            :viewer="{ memberId: campaign.currentMemberId, isDm }"
            :findNote="findNote" />

        <!-- One viewer for the whole stream, following `?image=` (16c). -->
        <ImageViewer
            :campaignId="campaignId"
            :items="viewerItems"
            :authorName="authorName"
            :ready="!!streamQuery.data.value && !streamQuery.isFetching.value" />

        <!-- One sheet for the whole stream, opened by a long-press on a note (14e). -->
        <SessionNoteActionSheet
            v-if="sheet"
            v-model:open="sheetOpen"
            :note="sheet.note"
            :actions="sheet.actions"
            :authorName="authorName(sheet.note.authorMemberId)"
            @select="(action, visibility) => sheet?.run(action, visibility)" />
    </div>
</template>

<script setup lang="ts">
    import { useInfiniteQuery } from "@tanstack/vue-query";
    import { useResizeObserver } from "@vueuse/core";
    import { ArrowDown, LoaderCircle } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { Campaign, SessionNote, SessionStreamFilter, Visibility } from "~/utils/api/types";
    import { currentMember } from "~/utils/campaign";
    import { getSessionStreamQuery } from "~/utils/queries/sessions";
    import { noteLinkProgress, type NoteAction } from "~/utils/noteActions";
    import { flattenSessions } from "~/utils/sessionStreamCache";
    import { filterEmptyState, visibleStreamSessions } from "~/utils/streamFilters";

    const props = withDefaults(
        defineProps<{
            campaignId: string;
            campaign: Campaign;
            filter?: SessionStreamFilter;
            /** A note to open at (`?note=` from a copied link, 14d). */
            focusNoteId?: string;
        }>(),
        { filter: "All" }
    );

    const streamQuery = useInfiniteQuery(
        getSessionStreamQuery(
            () => props.campaignId,
            () => props.filter
        )
    );

    const isDm = computed(() => currentMember(props.campaign)?.role === "DM");
    const usernames = computed(() => new Map(props.campaign.members.map((m) => [m.memberId, m.username])));
    const authorName = (memberId: string) => usernames.value.get(memberId) ?? "Unknown member";

    // Sessions oldest first, recaps lifted under the divider. Under a filter, a
    // session with no matching note has no divider, except the current one (14e).
    const visibleSessions = computed(() =>
        visibleStreamSessions(flattenSessions(streamQuery.data.value), props.filter).map((s) => ({
            session: s.session,
            recaps: s.notes.filter((n) => n.isRecap),
            notes: s.notes.filter((n) => !n.isRecap),
        }))
    );
    const allNotes = computed<SessionNote[]>(() => flattenSessions(streamQuery.data.value).flatMap((s) => s.notes));
    const noteCount = computed(() => allNotes.value.length);
    const findNote = (noteId: string) => allNotes.value.find((n) => n.id === noteId);

    // ── The image viewer (16c) ───────────────────────────────────────────────
    const imageViewer = useImageViewer();
    const viewerItems = computed(() =>
        flattenSessions(streamQuery.data.value).flatMap((s) =>
            s.notes.filter((n) => n.images.length > 0).map((note) => ({ note, sessionNumber: s.session.number }))
        )
    );
    const emptyState = computed(() => filterEmptyState(props.filter));

    // ── The long-press action sheet ──────────────────────────────────────────
    type SheetTarget = {
        note: SessionNote;
        actions: NoteAction[];
        run: (action: NoteAction, visibility?: Visibility) => Promise<void>;
    };
    const sheet = shallowRef<SheetTarget | null>(null);
    const sheetOpen = ref(false);
    function openSheet(note: SessionNote, target: Omit<SheetTarget, "note">) {
        sheet.value = { note, ...target };
        sheetOpen.value = true;
    }
    // The note left the stream (deleted, hidden from this viewer, filtered out).
    watch(allNotes, (notes) => {
        const open = sheet.value;
        if (open && !notes.some((n) => n.id === open.note.id)) sheetOpen.value = false;
    });

    // ── Scrolling ────────────────────────────────────────────────────────────
    // Newest at the bottom, like Discord. The stream opens at the bottom, stays
    // pinned there while the reader is at the bottom, and keeps the reader's place
    // when older sessions load above.
    const scroller = useTemplateRef<HTMLElement>("scroller");
    const content = useTemplateRef<HTMLElement>("content");
    const BOTTOM_SLACK = 80;
    const TOP_LOAD_AT = 200;

    const atBottom = ref(true);
    const hasUnseen = ref(false);

    function measureAtBottom() {
        const el = scroller.value;
        if (!el) return true;
        return el.scrollHeight - el.scrollTop - el.clientHeight < BOTTOM_SLACK;
    }

    function scrollToBottom(behavior: ScrollBehavior = "auto") {
        const el = scroller.value;
        if (!el) return;
        el.scrollTo({ top: el.scrollHeight, behavior });
        atBottom.value = true;
        hasUnseen.value = false;
    }

    // Loading older sessions: remember the distance from the bottom, then restore it
    // once they are drawn above.
    let restoreFromBottom: number | null = null;
    async function loadOlder() {
        const el = scroller.value;
        if (!el || !streamQuery.hasNextPage.value || streamQuery.isFetchingNextPage.value) return;
        restoreFromBottom = el.scrollHeight - el.scrollTop;
        await streamQuery.fetchNextPage();
        await nextTick();
        if (restoreFromBottom !== null && scroller.value) {
            scroller.value.scrollTop = scroller.value.scrollHeight - restoreFromBottom;
        }
        restoreFromBottom = null;
        fillViewport();
    }

    // A short stream loads older sessions until it fills the screen or runs out.
    function fillViewport() {
        const el = scroller.value;
        if (el && el.scrollHeight <= el.clientHeight && streamQuery.hasNextPage.value) void loadOlder();
    }

    function onScroll() {
        const el = scroller.value;
        if (!el) return;
        atBottom.value = measureAtBottom();
        if (atBottom.value) hasUnseen.value = false;
        if (el.scrollTop < TOP_LOAD_AT) void loadOlder();
    }

    // Content grows (a new note, fonts, a wrapped line): stay pinned to the bottom
    // when the reader was there; paging restores its own position above.
    useResizeObserver(content, () => {
        if (restoreFromBottom === null && atBottom.value) scrollToBottom();
    });
    // The stream itself shrinks when the composer is pinned above the keyboard (14e):
    // a reader at the bottom stays at the bottom.
    useResizeObserver(scroller, () => {
        if (restoreFromBottom === null && atBottom.value) scrollToBottom();
    });

    let knownIds = new Set<string>();
    let knownPages = 0;
    const opened = ref(false);

    // A different campaign or filter starts over at the bottom. Registered before
    // the data watchers so it runs first when both change in one tick.
    watch(
        () => [props.campaignId, props.filter],
        () => {
            opened.value = false;
            hasUnseen.value = false;
            knownIds = new Set();
            knownPages = 0;
        }
    );

    // First load: open at the bottom of the current session.
    watch(
        () => streamQuery.data.value,
        async (data) => {
            if (!data || opened.value) return;
            opened.value = true;
            await nextTick();
            scrollToBottom();
            fillViewport();
        },
        { immediate: true }
    );

    // New notes from others while scrolled up show the pill; one's own note, or any
    // note while at the bottom, scrolls into view. Notes that arrive with an older
    // page are not new.
    watch(allNotes, async (notes) => {
        const pages = streamQuery.data.value?.pages.length ?? 0;
        const paged = pages > knownPages;
        const fresh = paged ? [] : notes.filter((n) => !knownIds.has(n.id));
        knownIds = new Set(notes.map((n) => n.id));
        knownPages = pages;
        if (fresh.length === 0 || !opened.value) return;

        const mine = fresh.some((n) => n.authorMemberId === props.campaign.currentMemberId);
        if (atBottom.value || mine) {
            await nextTick();
            scrollToBottom(mine ? "smooth" : "auto");
        } else {
            hasUnseen.value = true;
        }
    });

    // ── Note links ───────────────────────────────────────────────────────────
    // `?note={id}`: load older pages until the note's session is loaded, then scroll
    // to the note and highlight it. A note the viewer cannot see is a 404.
    const emit = defineEmits<{ noteOpened: [noteId: string] }>();
    const highlightedId = ref<string | null>(null);
    let highlightTimer: ReturnType<typeof setTimeout> | undefined;

    async function goToNote(noteId: string) {
        let sessionNumber: number;
        try {
            sessionNumber = (
                await useApi().note.get({
                    campaignId: props.campaignId,
                    noteId,
                })
            ).sessionNumber;
        } catch {
            toast.error("That note is not there, or you cannot see it.");
            emit("noteOpened", noteId);
            return;
        }
        // Bounded, in case the server keeps answering `hasOlder` without older sessions.
        for (let pages = 0; pages < 500; pages++) {
            const progress = noteLinkProgress(
                flattenSessions(streamQuery.data.value),
                sessionNumber,
                !!streamQuery.hasNextPage.value
            );
            if (progress !== "more") break;
            if (streamQuery.isFetchingNextPage.value) {
                // A page is already on its way (the stream filling the screen).
                await new Promise((resolve) => setTimeout(resolve, 50));
                continue;
            }
            await loadOlder();
        }
        await nextTick();
        const el = document.getElementById(`note-${noteId}`);
        if (el) {
            atBottom.value = false;
            el.scrollIntoView({ block: "center" });
            highlightedId.value = noteId;
            clearTimeout(highlightTimer);
            highlightTimer = setTimeout(() => (highlightedId.value = null), 2500);
        } else {
            toast.info("That note is not shown under this filter.");
        }
        emit("noteOpened", noteId);
    }

    // Once the stream has opened, and again whenever a new link is followed.
    watch(
        () => [opened.value, props.focusNoteId] as const,
        ([isOpen, noteId]) => {
            if (isOpen && noteId) void goToNote(noteId);
        },
        { immediate: true }
    );
    onBeforeUnmount(() => clearTimeout(highlightTimer));

    defineExpose({ scrollToBottom, goToNote });
</script>
