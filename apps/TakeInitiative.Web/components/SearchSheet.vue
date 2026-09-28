<template>
    <DialogRoot v-model:open="open">
        <DialogPortal>
            <!-- Full screen on a phone; a centred panel over the page on desktop. -->
            <DialogOverlay
                class="fixed inset-0 z-50 hidden bg-black/80 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 md:block" />
            <DialogContent
                class="fixed inset-0 z-50 flex flex-col bg-background pt-safe px-safe pb-safe data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 md:inset-x-4 md:bottom-auto md:top-[10vh] md:mx-auto md:h-fit md:max-h-[70vh] md:max-w-xl md:rounded-lg md:border md:shadow-lg"
                @openAutoFocus="focusInput"
                @closeAutoFocus="restoreFocus">
                <div class="relative flex shrink-0 items-center gap-2 border-b px-2">
                    <Search class="ml-2 size-5 shrink-0 text-muted-foreground" />
                    <DialogTitle class="sr-only">Search</DialogTitle>
                    <DialogDescription class="sr-only">
                        Search this campaign.
                    </DialogDescription>
                    <input
                        ref="input"
                        v-model="query"
                        type="search"
                        enterkeyhint="search"
                        placeholder="Search"
                        autocomplete="off"
                        autocapitalize="off"
                        spellcheck="false"
                        role="combobox"
                        aria-autocomplete="list"
                        :aria-expanded="rows.length > 0"
                        :aria-controls="listboxId"
                        :aria-activedescendant="activeRow ? optionId(activeRow.id) : undefined"
                        class="h-14 min-w-0 flex-1 bg-transparent text-base outline-none placeholder:text-muted-foreground"
                        @keydown="onKeydown" />
                    <DialogClose
                        class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        aria-label="Close search">
                        <X class="size-5" />
                    </DialogClose>
                    <!-- Loading: a thin bar; the previous results stay under it. -->
                    <div
                        v-if="loading"
                        class="absolute inset-x-0 -bottom-px h-0.5 overflow-hidden"
                        role="progressbar"
                        aria-label="Searching">
                        <div class="h-full w-1/3 animate-[search-bar_1s_ease-in-out_infinite] bg-gold" />
                    </div>
                </div>

                <!-- The keyboard's height as bottom padding, so no row is ever under it;
                     a touch scroll drops the keyboard to show more rows. -->
                <div
                    class="min-h-0 flex-1 overflow-y-auto overscroll-contain pb-2"
                    :style="{ paddingBottom: `${keyboardInset + 8}px` }"
                    @touchmove.passive="dropKeyboard">
                    <SearchResults
                        v-if="rows.length > 0"
                        v-model:cursor="cursor"
                        :campaignId="campaignId"
                        :rows="rows"
                        :listboxId="listboxId"
                        :optionId="optionId"
                        :authorName="authorName"
                        :loadingMore="loadingMore"
                        @choose="(row) => choose(row, 'pointer')">
                        <template #action="{ row }">
                            <SearchCreateEntryRow
                                v-if="row.actionId === CREATE_ENTRY_ACTION_ID"
                                v-model:kind="create.kind"
                                v-model:visibility="create.visibility"
                                :campaignId="campaignId"
                                :label="row.label"
                                :expanded="create.expanded"
                                :pending="createEntry.isPending.value"
                                :error="create.error"
                                :existing="existingEntry"
                                @create="createFromRow"
                                @openExisting="open = false" />
                            <SearchActionRow
                                v-else
                                :icon="row.icon"
                                :label="row.label"
                                :pending="row.actionId === START_SESSION_ACTION_ID && startSession.isPending.value" />
                        </template>
                    </SearchResults>
                    <div
                        v-else
                        :id="listboxId"
                        role="listbox"
                        aria-label="Search results" />

                    <div
                        v-if="message"
                        class="flex flex-col items-center gap-2 px-6 py-6 text-center text-sm text-muted-foreground"
                        role="status">
                        <p>{{ message }}</p>
                        <Button
                            v-if="failed"
                            variant="outline"
                            size="sm"
                            class="h-11 md:h-9"
                            @click="searchQuery.refetch()">
                            Retry
                        </Button>
                    </div>
                </div>
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { useQuery, useQueryClient } from "@tanstack/vue-query";
    import { refDebounced } from "@vueuse/core";
    import { Search, X } from "lucide-vue-next";
    import {
        DialogClose,
        DialogContent,
        DialogDescription,
        DialogOverlay,
        DialogPortal,
        DialogRoot,
        DialogTitle,
    } from "reka-ui";
    import { toast } from "vue-sonner";
    import type { EntryKind, EntrySummary, SearchSection, SearchSectionKey, Visibility } from "~/utils/api/types";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import { currentMember } from "~/utils/campaign";
    import { nextSessionNumber } from "~/utils/composer";
    import { existingEntryIdFrom, resolveEntry } from "~/utils/entries";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { createEntryMutation, useEntryDirectory } from "~/utils/queries/entries";
    import { getSearchQuery, getSearchSectionQuery } from "~/utils/queries/search";
    import { getSessionsQuery, startSessionMutation } from "~/utils/queries/sessions";
    import {
        CREATE_ENTRY_ACTION_ID,
        SEARCH_ACTIONS,
        START_SESSION_ACTION_ID,
        cycleEntryKind,
        cycleEntryVisibility,
        searchActions,
        type SearchActionContext,
    } from "~/utils/searchActions";
    import {
        actionRows,
        firstRow,
        hitTarget,
        isEmptyResponse,
        moveCursor,
        parseSearchInput,
        readRecentIds,
        recentEntries,
        recentRows,
        rememberRecentEntry,
        replaceSection,
        searchParams,
        searchRows,
        type SearchRow,
    } from "~/utils/search";

    const props = defineProps<{
        campaignId: string;
        /** Where focus goes when the sheet closes (the 🔍 button, or what ⌘K left). */
        returnFocus?: HTMLElement | null;
    }>();
    const open = defineModel<boolean>("open", { required: true });

    const query = ref("");
    const input = useTemplateRef<HTMLInputElement>("input");
    const listboxId = useId();
    const optionId = (rowId: string) => `${listboxId}-${rowId}`;

    function focusInput(event: Event) {
        event.preventDefault();
        input.value?.focus();
    }
    // An action that opens the composer (✎) hands it the focus, so it is not taken back.
    let handedOff = false;
    function restoreFocus(event: Event) {
        event.preventDefault();
        if (handedOff) {
            handedOff = false;
            return;
        }
        props.returnFocus?.focus({ preventScroll: true });
    }

    // ── Querying ─────────────────────────────────────────────────────────────
    // What is typed now decides between the recents and the results at once; the
    // request follows 120 ms after the last keystroke.
    const typed = computed(() => parseSearchInput(query.value));
    const debounced = refDebounced(query, 120);
    const searchInput = computed(() => parseSearchInput(debounced.value));
    const searching = computed(() => !!searchParams(typed.value));

    const searchQuery = useQuery(getSearchQuery(() => props.campaignId, searchInput));
    const result = computed(() => (searching.value ? searchQuery.data.value : undefined));
    const resultKey = computed(() => (result.value ? `${result.value.scope}|${result.value.text}` : ""));

    // "Show more": one section at a time, replaced in place, for the result shown.
    const queryClient = useQueryClient();
    const expanded = shallowRef<{ key: string; sections: SearchSection[] }>({ key: "", sections: [] });
    const loadingMore = ref<SearchSectionKey | null>(null);
    const expandedFor = computed(() => (expanded.value.key === resultKey.value ? expanded.value.sections : []));
    const response = computed(() =>
        result.value ? expandedFor.value.reduce(replaceSection, result.value.response) : undefined
    );

    async function showMore(section: SearchSectionKey, at: number) {
        const shown = result.value;
        if (!shown || loadingMore.value) return;
        loadingMore.value = section;
        try {
            const more = await queryClient.fetchQuery(getSearchSectionQuery(props.campaignId, shown, section));
            if (!more || resultKey.value !== `${shown.scope}|${shown.text}`) return;
            const kept = expanded.value.key === resultKey.value ? expanded.value.sections : [];
            expanded.value = { key: resultKey.value, sections: [...kept.filter((s) => s.key !== section), more] };
            // The cursor stays where "Show more" was: on the first new hit.
            await nextTick();
            cursor.value = rows.value[at] && rows.value[at].type !== "header" ? at : firstRow(rows.value);
        } catch {
            toast.error("Could not load more.");
        } finally {
            loadingMore.value = null;
        }
    }

    // ── Recent entries (opened from ⌘K) ─────────────────────────────────────
    const directory = useEntryDirectory(() => props.campaignId);
    const recentIds = ref<string[]>([]);
    const storage = () => {
        try {
            return window.localStorage;
        } catch {
            return undefined;
        }
    };

    const campaignQuery = useQuery(getCampaignQuery(() => props.campaignId));
    const authorName = (memberId: string) =>
        campaignQuery.data.value?.members.find((m) => m.memberId === memberId)?.username ?? "Unknown member";

    // ── Actions (17c) ────────────────────────────────────────────────────────
    const sessionsQuery = useQuery(getSessionsQuery(() => props.campaignId));
    // "Post a note about X" is about the highlighted entry hit, else the top one. It
    // keeps the last one highlighted, so moving onto the action does not change it.
    const lastEntryHit = shallowRef<EntrySummary | null>(null);
    const topEntryHit = computed(
        () => response.value?.sections.find((s) => s.key === "Entries")?.hits[0]?.entry?.entry ?? null
    );
    const actionContext = computed<SearchActionContext>(() => {
        const campaign = campaignQuery.data.value;
        const sessions = sessionsQuery.data.value;
        return {
            campaignId: props.campaignId,
            isDm: campaign ? currentMember(campaign)?.role === "DM" : false,
            nextSessionNumber: sessions ? nextSessionNumber(sessions.sessions) : null,
            hasSession: (sessions?.sessions.length ?? 0) > 0,
            scope: typed.value.scope,
            text: typed.value.text,
            directory: directory.value,
            entryHit: lastEntryHit.value ?? topEntryHit.value,
        };
    });
    const actions = computed(() =>
        actionRows(
            searchActions(actionContext.value).map((a) => ({ id: a.id, icon: a.icon, label: a.label(actionContext.value) }))
        )
    );

    // ── Rows and the cursor ──────────────────────────────────────────────────
    // The sections, then Actions. `>` lists actions only, and an empty input the
    // recent entries and the default actions.
    const rows = computed<SearchRow[]>(() => {
        if (typed.value.scope === "actions") return actions.value;
        if (!typed.value.text) {
            return typed.value.scope === "all"
                ? [...recentRows(recentEntries(recentIds.value, directory.value)), ...actions.value]
                : [];
        }
        const shownFully = new Set(expandedFor.value.map((s) => s.key));
        const found = searching.value && response.value ? searchRows(response.value, shownFully) : [];
        return [...found, ...actions.value];
    });
    const cursor = ref(-1);
    const activeRow = computed(() => rows.value[cursor.value]);
    // A new answer (or the recents, or another `>` query) starts at the first row.
    watch(
        () =>
            [
                resultKey.value,
                typed.value.scope,
                typed.value.scope === "actions" ? typed.value.text : "",
                typed.value.text === "",
                rows.value.length > 0,
            ] as const,
        () => (cursor.value = firstRow(rows.value)),
        { immediate: true }
    );
    watch(resultKey, () => (lastEntryHit.value = null));
    watch(activeRow, (row) => {
        if (row?.type === "hit" && row.hit.entry) lastEntryHit.value = row.hit.entry.entry;
    });
    watch(
        () => activeRow.value?.id,
        async (id) => {
            if (!id) return;
            await nextTick();
            document.getElementById(optionId(id))?.scrollIntoView({ block: "nearest" });
        }
    );

    // ── States ───────────────────────────────────────────────────────────────
    const loading = computed(
        () => searching.value && (searchQuery.isFetching.value || debounced.value !== query.value)
    );
    const failed = computed(() => searching.value && searchQuery.isError.value && !loading.value);
    const message = computed(() => {
        if (!typed.value.text) {
            return typed.value.scope === "all"
                ? "Search entries, notes, images, sessions and combats. @ for entries, > for actions."
                : typed.value.scope === "entries"
                  ? "Search entries."
                  : null;
        }
        if (typed.value.scope === "actions") {
            return actions.value.length === 0 ? `No actions match “${typed.value.text}”.` : null;
        }
        if (failed.value) return "Search failed.";
        const shown = result.value;
        if (
            shown &&
            !searchQuery.isPlaceholderData.value &&
            !loading.value &&
            shown.text === typed.value.text &&
            isEmptyResponse(shown.response)
        ) {
            return `Nothing found for “${typed.value.text}”.`;
        }
        return null;
    });

    // ── Choosing a row ───────────────────────────────────────────────────────
    /** Enter (`key`) or a tap or click (`pointer`): the Create row only opens on a tap. */
    function choose(row: SearchRow | undefined, via: "key" | "pointer" = "key") {
        if (!row || row.type === "header") return;
        if (row.type === "action") {
            runAction(row.actionId, via);
            return;
        }
        if (row.type === "more") {
            void showMore(row.section, rows.value.indexOf(row));
            return;
        }
        if (row.hit.entry) recentIds.value = rememberRecentEntry(storage(), props.campaignId, row.hit.entry.entry.id);
        open.value = false;
        void navigateTo(hitTarget(props.campaignId, row.hit));
    }

    function runAction(actionId: string, via: "key" | "pointer") {
        const action = SEARCH_ACTIONS.find((a) => a.id === actionId);
        if (!action) return;
        const run = action.run(actionContext.value);
        switch (run.kind) {
            case "createEntry":
                if (via === "pointer") create.expanded = !create.expanded;
                else void createFromRow();
                return;
            case "startSession":
                void start(run.number);
                return;
            case "navigate":
                handedOff = !!run.composer;
                open.value = false;
                void navigateTo(run.target);
        }
    }

    // ── Create entry (17c) ───────────────────────────────────────────────────
    const create = reactive({
        kind: "Character" as EntryKind,
        visibility: "Everyone" as Visibility,
        expanded: false,
        error: null as string | null,
        existingId: null as string | null,
    });
    watch(
        () => typed.value.text,
        () => {
            create.expanded = false;
            create.error = null;
            create.existingId = null;
        }
    );
    // A 409 names an entry the viewer can see; a hidden one of the same name never blocks.
    const existingEntry = computed(() =>
        create.existingId ? (resolveEntry(directory.value, create.existingId) ?? null) : null
    );
    const createEntry = createEntryMutation();
    async function createFromRow() {
        const name = typed.value.text;
        if (!name || createEntry.isPending.value) return;
        try {
            // The mutation adds the entry to the directory, so ⌘K finds it at once.
            const entry = await createEntry.mutateAsync({
                campaignId: props.campaignId,
                name,
                kind: create.kind,
                visibility: create.visibility,
            });
            recentIds.value = rememberRecentEntry(storage(), props.campaignId, entry.id);
            open.value = false;
            await navigateTo(`/app/campaigns/${encodeURIComponent(props.campaignId)}/wiki/${encodeURIComponent(entry.id)}`);
        } catch (error) {
            create.existingId = apiErrorStatus(error) === 409 ? existingEntryIdFrom(error) : null;
            create.error = apiErrorMessage(error, "Could not create the entry.");
        }
    }

    // ── Start Session N+1 (17c) ──────────────────────────────────────────────
    const startSession = startSessionMutation();
    async function start(number: number) {
        if (startSession.isPending.value) return;
        try {
            const session = await startSession.mutateAsync({ campaignId: props.campaignId, number });
            open.value = false;
            toast.success(`Session ${session.number} started`);
            await navigateTo(`/app/campaigns/${encodeURIComponent(props.campaignId)}`);
        } catch (error) {
            // A 409: another member was first. The mutation reads the sessions again.
            toast.error(
                apiErrorStatus(error) === 409
                    ? apiErrorMessage(error, "Someone else started a session. Try again.")
                    : apiErrorMessage(error, "Could not start the session.")
            );
        }
    }

    function onKeydown(event: KeyboardEvent) {
        if (event.isComposing) return;
        // On the Create row, Tab cycles the kind and Shift+Tab the visibility (§3).
        if (event.key === "Tab" && activeRow.value?.type === "action" && activeRow.value.actionId === CREATE_ENTRY_ACTION_ID) {
            event.preventDefault();
            if (event.shiftKey) create.visibility = cycleEntryVisibility(create.visibility);
            else create.kind = cycleEntryKind(create.kind);
            return;
        }
        if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            cursor.value = moveCursor(rows.value, cursor.value, event.key === "ArrowDown" ? 1 : -1);
        } else if (event.key === "Enter") {
            // The keyboard's Search key too: the highlighted row, the first by default.
            event.preventDefault();
            // Typed and Enter before the hits arrive: never a Create nobody saw offered.
            const row = activeRow.value ?? rows.value[firstRow(rows.value)];
            if (loading.value && row?.type === "action" && row.actionId === CREATE_ENTRY_ACTION_ID) return;
            choose(row);
        }
    }

    // ── Mobile ───────────────────────────────────────────────────────────────
    const keyboardInset = useKeyboardInset();
    function dropKeyboard() {
        if (document.activeElement === input.value) input.value?.blur();
    }

    // Each open starts with an empty query and the recents as they are now.
    watch(
        open,
        (isOpen) => {
            if (!isOpen) return;
            query.value = "";
            expanded.value = { key: "", sections: [] };
            handedOff = false;
            Object.assign(create, { kind: "Character", visibility: "Everyone", expanded: false, error: null, existingId: null });
            recentIds.value = readRecentIds(storage(), props.campaignId);
        },
        { immediate: true }
    );
</script>

<style>
    @keyframes search-bar {
        from {
            transform: translateX(-100%);
        }
        to {
            transform: translateX(300%);
        }
    }
</style>
