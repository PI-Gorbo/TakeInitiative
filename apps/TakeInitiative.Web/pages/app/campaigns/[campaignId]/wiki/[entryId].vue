<template>
    <PageContainer class="flex flex-col gap-5 px-4 py-4 pb-safe">
        <!-- An entry (design §4): its header (15c), its source (20d), its claim and stats (15g), the
             Summary | Notes tabs (25d: its article, 15f, and its timeline, 15c), its connections
             (19c), its gallery (16d) and its combats (18e). `?edit={blockId}` opens the article
             editor at a block (a phone's promote, §3a). A merged entry's id loads its target
             (15g), and the URL is replaced with the target's. -->
        <NuxtLink
            :to="`/app/campaigns/${encodeURIComponent(campaignId)}/wiki`"
            class="-ml-2 flex h-11 w-fit items-center gap-1 rounded-md px-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground md:h-9">
            <ChevronLeft
                class="size-4"
                aria-hidden="true" />
            Wiki
        </NuxtLink>

        <EmptyState
            v-if="notFound"
            :icon="BookX"
            title="Entry not found">
            That entry is not there, or you cannot see it.
        </EmptyState>
        <LoadingFallback
            v-else-if="!entry || !campaign"
            :isLoading="entryQuery.isLoading.value || !campaign"
            :isError="entryQuery.isError.value"
            iconSize="2x"
            class="pt-8" />
        <template v-else>
            <WikiEntryHeaderEditor
                v-if="editing"
                :key="entry.id"
                :campaignId="campaignId"
                :entry="entry"
                :canEdit="canEdit"
                :canChangeAccess="canChangeAccess"
                @done="editing = false" />
            <WikiEntryHeader
                v-else
                :entry="entry"
                :viewerMemberId="campaign.currentMemberId"
                :creatorName="creatorName"
                :canEdit="canEdit"
                :canChangeAccess="canChangeAccess"
                :editing="editing"
                :claimerName="entry.claimedByMemberId ? memberName(entry.claimedByMemberId) : undefined"
                @edit="editing = true"
                @history="historyOpen = true"
                @merge="mergeOpen = true" />

            <ReferenceEntrySourceLine
                v-if="entry.source"
                :campaignId="campaignId"
                :source="entry.source" />
            <WikiClaimControl
                :campaignId="campaignId"
                :entry="entry"
                :viewer="viewer"
                :members="campaign.members"
                :nameOf="memberName" />
            <WikiStatsEditor
                :key="`${entry.id}-stats`"
                :campaignId="campaignId"
                :entry="entry"
                :viewer="viewer" />

            <!-- Summary | Notes (25d): the article and the timeline, in the UI's words.
                 Both panels stay mounted, so switching never drops an open editor. -->
            <Tabs
                :id="ENTRY_TIMELINE_ANCHOR"
                :modelValue="tab"
                :unmountOnHide="false"
                class="flex scroll-mt-4 flex-col gap-3"
                @update:modelValue="(value) => selectTab(value)">
                <TabsList class="grid h-11 w-full grid-cols-2 md:h-10 md:w-fit">
                    <TabsTrigger
                        value="summary"
                        class="h-9 md:h-8">
                        Summary
                    </TabsTrigger>
                    <TabsTrigger
                        value="notes"
                        class="h-9 md:h-8">
                        Notes<template v-if="noteCount !== null"> · {{ noteCount }}</template>
                    </TabsTrigger>
                </TabsList>
                <TabsContent
                    value="summary"
                    class="mt-0">
                    <WikiArticleEditor
                        v-if="articleEditing && canEdit"
                        :key="`${entry.id}-editor`"
                        :campaignId="campaignId"
                        :entry="entry"
                        :viewer="viewer"
                        :nameOf="memberName"
                        :focusBlockId="focusBlockId"
                        :restore="restoring"
                        @done="closeArticleEditor" />
                    <WikiArticle
                        v-else
                        :campaignId="campaignId"
                        :article="entry.article"
                        :viewerMemberId="viewer.memberId"
                        :canEdit="canEdit"
                        :nameOf="memberName"
                        :highlightedBlockId="highlightedBlockId"
                        :entryName="entry.name"
                        :noteCount="noteCount"
                        @edit="openArticleEditor()"
                        @pickNotes="selectTab('notes')"
                        @openNote="openNote" />
                </TabsContent>
                <TabsContent
                    value="notes"
                    class="mt-0">
                    <WikiEntryTimeline
                        :campaign="campaign"
                        :entryId="entry.id"
                        :entryName="entry.name"
                        :canEdit="canEdit"
                        :inSummary="inSummary"
                        :highlightedNoteId="highlightedNoteId" />
                </TabsContent>
            </Tabs>

            <WikiEntryConnections
                :campaignId="campaignId"
                :entryId="entry.id"
                :entryName="entry.name"
                :entryKind="entry.kind"
                :nameOf="memberName" />

            <WikiEntryGallery
                :campaign="campaign"
                :entryId="entry.id" />

            <CombatEntryCombats
                :campaignId="campaignId"
                :entryId="entry.id" />

            <WikiEntryHistoryDialog
                v-model:open="historyOpen"
                :campaignId="campaignId"
                :entryId="entry.id"
                :entryName="entry.name"
                :viewerMemberId="viewer.memberId"
                :canEdit="canEdit"
                :nameOf="memberName"
                @restore="restoreVersion" />
            <WikiMergeDialog
                v-if="canEdit"
                v-model:open="mergeOpen"
                :campaignId="campaignId"
                :entry="entry"
                :viewer="viewer"
                :members="campaign.members"
                :nameOf="memberName" />
        </template>
    </PageContainer>
</template>

<script setup lang="ts">
    import { useInfiniteQuery, useQuery } from "@tanstack/vue-query";
    import { BookX, ChevronLeft } from "lucide-vue-next";
    import { apiErrorStatus } from "~/utils/apiErrorParser";
    import { currentMember } from "~/utils/campaign";
    import type { ArticleBlock } from "~/utils/api/types";
    import {
        EDIT_BLOCK_PARAM,
        ENTRY_TAB_PARAM,
        ENTRY_TIMELINE_ANCHOR,
        entryHref,
        initialEntryTab,
        isEntryTab,
        quotedNoteIds,
        type EntryTab,
    } from "~/utils/article";
    import { timelineItems } from "~/utils/entryCache";
    import { canChangeEntryAccess, canEditEntry } from "~/utils/entries";
    import { BLOCK_LINK_PARAM } from "~/utils/search";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getEntryQuery, getEntryTimelineQuery } from "~/utils/queries/entries";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const route = useRoute("app-campaigns-campaignId-wiki-entryId");
    const campaignId = computed(() => route.params.campaignId as string);
    const entryId = computed(() => route.params.entryId as string);

    const campaignQuery = useQuery(getCampaignQuery(campaignId));
    const campaign = computed(() => campaignQuery.data.value);
    const entryQuery = useQuery(getEntryQuery(campaignId, entryId));
    const entry = computed(() => entryQuery.data.value);
    const notFound = computed(() => apiErrorStatus(entryQuery.error.value) === 404);

    useHead({
        title: () => (entry.value ? `${entry.value.name} · Wiki` : "Wiki"),
    });

    const viewer = computed(() => ({
        memberId: campaign.value?.currentMemberId ?? "",
        isDm: currentMember(campaign.value)?.role === "DM",
    }));
    const canEdit = computed(() => !!entry.value && canEditEntry(entry.value, viewer.value));
    const canChangeAccess = computed(() => !!entry.value && canChangeEntryAccess(entry.value, viewer.value));
    const creatorName = computed(
        () => campaign.value?.members.find((m) => m.memberId === entry.value?.creatorMemberId)?.username ?? "the creator"
    );

    const memberName = (memberId: string) =>
        campaign.value?.members.find((m) => m.memberId === memberId)?.username ?? "Unknown member";

    const editing = ref(false);
    // Losing the right to edit (edit access or a role change) closes the editor.
    watch([canEdit, canChangeAccess], ([edit, access]) => {
        if (!edit && !access) editing.value = false;
    });

    // ── The article editor (15f) ─────────────────────────────────────────────
    const articleEditing = ref(false);
    const focusBlockId = ref<string | undefined>();
    // An old version the editor starts from ("Restore this version", 15g).
    const restoring = ref<{ blocks: readonly ArticleBlock[]; label: string } | undefined>();
    function openArticleEditor(blockId?: string) {
        focusBlockId.value = blockId;
        restoring.value = undefined;
        articleEditing.value = true;
    }
    function closeArticleEditor() {
        articleEditing.value = false;
        restoring.value = undefined;
    }

    // ── Summary | Notes (25d) ────────────────────────────────────────────────
    // The tab is chosen once the entry has loaded (`initialEntryTab`: `?edit=` and
    // `?block=` force Summary, then `?tab=`, then `#timeline`, else Summary unless it is
    // empty), and a switch writes `?tab=` with `replace`.
    const tab = ref<EntryTab | undefined>();
    watch(
        entry,
        (loaded) => {
            if (!loaded || tab.value) return;
            tab.value = initialEntryTab({
                hasSummary: loaded.article.blocks.length > 0,
                tab: route.query[ENTRY_TAB_PARAM],
                hash: route.hash,
                forceSummary:
                    route.query[EDIT_BLOCK_PARAM] !== undefined || route.query[BLOCK_LINK_PARAM] !== undefined,
            });
        },
        { immediate: true }
    );
    function selectTab(value: unknown) {
        if (!isEntryTab(value) || value === tab.value) return;
        tab.value = value;
        // `#timeline` has done its job; `?tab=` carries the choice from here.
        void navigateTo({ query: { ...route.query, [ENTRY_TAB_PARAM]: value }, hash: "" }, { replace: true });
    }

    // The same query as the Notes tab's (one cache entry): its count, once every page has
    // loaded (the API has no total), and the pages a source chip may have to fetch.
    const timelineQuery = useInfiniteQuery(getEntryTimelineQuery(campaignId, () => entry.value?.id ?? ""));
    const noteCount = computed(() =>
        timelineQuery.data.value && !timelineQuery.hasNextPage.value
            ? timelineItems(timelineQuery.data.value).length
            : null
    );
    const inSummary = computed(() => quotedNoteIds(entry.value?.article.blocks));

    // A quote's source chip: the Notes tab, scrolled to the note, marked for a moment. A
    // note older than the loaded pages is fetched first (a few pages at most).
    const highlightedNoteId = ref<string | null>(null);
    let noteTimer: ReturnType<typeof setTimeout> | undefined;
    const MAX_PAGES_FOR_NOTE = 10;
    async function openNote(noteId: string) {
        selectTab("notes");
        const loaded = () =>
            timelineItems(timelineQuery.data.value).some((i) => i.note.id.toLowerCase() === noteId.toLowerCase());
        for (let i = 0; i < MAX_PAGES_FOR_NOTE && !loaded() && timelineQuery.hasNextPage.value; i++) {
            await timelineQuery.fetchNextPage();
        }
        await nextTick();
        const el = document.getElementById(`note-${noteId}`);
        if (!el) return;
        el.scrollIntoView({ block: "center" });
        highlightedNoteId.value = noteId;
        clearTimeout(noteTimer);
        noteTimer = setTimeout(() => (highlightedNoteId.value = null), 2500);
    }

    // ── History, restore and merge (15g) ─────────────────────────────────────
    const historyOpen = ref(false);
    const mergeOpen = ref(false);
    function restoreVersion(version: { blocks: readonly ArticleBlock[]; label: string }) {
        if (!canEdit.value) return;
        focusBlockId.value = undefined;
        // A new key remounts an open editor on the version.
        articleEditing.value = false;
        restoring.value = version;
        void nextTick(() => (articleEditing.value = true));
    }
    // A merged entry's id redirects: the server answers with the target, whose URL
    // replaces this one (the query, such as `?edit=`, is kept).
    watch(entry, (loaded) => {
        if (loaded && loaded.id.toLowerCase() !== entryId.value.toLowerCase()) {
            void navigateTo({ path: entryHref(campaignId.value, loaded.id), query: route.query }, { replace: true });
        }
    });
    watch(canEdit, (edit) => {
        if (!edit) articleEditing.value = false;
    });
    watch(entryId, () => {
        tab.value = undefined;
        highlightedNoteId.value = null;
        editing.value = false;
        closeArticleEditor();
        historyOpen.value = false;
        mergeOpen.value = false;
    });
    // `?edit={blockId}` is used once, when the entry has loaded, then dropped.
    watch(
        [() => route.query[EDIT_BLOCK_PARAM], entry, campaign, canEdit],
        ([blockId, loaded, member, edit]) => {
            if (typeof blockId !== "string" || !loaded || !member) return;
            if (edit) openArticleEditor(blockId);
            tab.value = "summary";
            const { [EDIT_BLOCK_PARAM]: _, ...query } = route.query;
            void navigateTo({ query }, { replace: true });
        },
        { immediate: true }
    );

    // `#timeline` (a loose end's "Pick from notes", 19e): once the entry has loaded, the
    // page opens its Notes tab, where each note has "Add to summary", and scrolls to it.
    watch(
        [() => route.hash, entry],
        async ([hash, loaded]) => {
            if (hash !== `#${ENTRY_TIMELINE_ANCHOR}` || !loaded) return;
            if (!isEntryTab(route.query[ENTRY_TAB_PARAM])) tab.value = "notes";
            await nextTick();
            document.getElementById(ENTRY_TIMELINE_ANCHOR)?.scrollIntoView({ block: "start" });
        },
        { immediate: true }
    );

    // `?block={blockId}` from ⌘K (17b) is used once, when the entry has loaded: the read
    // article scrolls to the block and marks it for a moment, then the parameter is
    // dropped. A block the viewer cannot see is simply not there.
    const highlightedBlockId = ref<string | null>(null);
    let blockTimer: ReturnType<typeof setTimeout> | undefined;
    watch(
        [() => route.query[BLOCK_LINK_PARAM], entry],
        async ([blockId, loaded]) => {
            if (typeof blockId !== "string" || !loaded) return;
            const { [BLOCK_LINK_PARAM]: _, ...query } = route.query;
            void navigateTo({ query }, { replace: true });
            tab.value = "summary";
            await nextTick();
            const el = document.getElementById(`block-${blockId}`);
            if (!el) return;
            el.scrollIntoView({ block: "center" });
            highlightedBlockId.value = blockId;
            clearTimeout(blockTimer);
            blockTimer = setTimeout(() => (highlightedBlockId.value = null), 2500);
        },
        { immediate: true }
    );
    onBeforeUnmount(() => {
        clearTimeout(blockTimer);
        clearTimeout(noteTimer);
    });
</script>
