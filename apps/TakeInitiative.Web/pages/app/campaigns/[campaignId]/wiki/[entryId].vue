<template>
    <!-- An entry (design §4): its header (15c), its claim and stats (15g), its article
         (15f) and its timeline (15c). Connections (19), gallery (16) and combats (18)
         arrive with their steps. `?edit={blockId}` opens the article editor at a block
         (a phone's promote, §3a). A merged entry's id loads its target (15g), and the
         URL is replaced with the target's. -->
    <div class="mx-auto flex w-full max-w-3xl flex-col gap-5 px-4 py-4 pb-safe">
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
                @edit="openArticleEditor()" />

            <WikiEntryTimeline
                :campaign="campaign"
                :entryId="entry.id"
                :entryName="entry.name"
                :canEdit="canEdit" />

            <!-- Posts to the current session with the mention prefilled (design §4). -->
            <Button
                as-child
                variant="outline"
                class="h-12 justify-start gap-2 text-muted-foreground md:h-10">
                <NuxtLink :to="aboutLink">
                    <MessageSquarePlus
                        class="size-4"
                        aria-hidden="true" />
                    <span class="truncate">Add a note about {{ entry.name }}…</span>
                </NuxtLink>
            </Button>

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
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { BookX, ChevronLeft, MessageSquarePlus } from "lucide-vue-next";
    import { apiErrorStatus } from "~/utils/apiErrorParser";
    import { currentMember } from "~/utils/campaign";
    import type { ArticleBlock } from "~/utils/api/types";
    import { EDIT_BLOCK_PARAM, entryHref } from "~/utils/article";
    import { ABOUT_PARAM, canChangeEntryAccess, canEditEntry } from "~/utils/entries";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getEntryQuery } from "~/utils/queries/entries";

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
            const { [EDIT_BLOCK_PARAM]: _, ...query } = route.query;
            void navigateTo({ query }, { replace: true });
        },
        { immediate: true }
    );

    const aboutLink = computed(
        () => `/app/campaigns/${encodeURIComponent(campaignId.value)}?${ABOUT_PARAM}=${encodeURIComponent(entryId.value)}`
    );
</script>
