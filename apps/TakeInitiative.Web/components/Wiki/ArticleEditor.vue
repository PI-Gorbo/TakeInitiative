<template>
    <!-- Editing an entry's article (15f, design §4 and §3a), one mode for the whole
         article. Each block is an `ArticleBlockEditor`; 🔒 splits a selection into a
         secret block. Save sends the viewer's whole view with the etag it was loaded
         with; blocks the viewer cannot see stay on the server, in place. A stale save
         opens `ConflictDialog` (reload and re-apply). A change pushed while editing
         shows "This article changed" and never replaces the text being written. -->
    <section
        aria-label="Edit the article"
        class="flex flex-col gap-3 pb-24 md:pb-0">
        <div class="sticky top-0 z-10 -mx-4 flex items-center gap-2 border-b bg-background/95 px-4 py-2 backdrop-blur">
            <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Editing article</h3>
            <div class="flex-1" />
            <Button
                variant="ghost"
                class="h-11 md:h-8"
                :disabled="saving"
                @click="cancel">
                {{ confirmDiscard ? "Discard changes?" : "Cancel" }}
            </Button>
            <Button
                class="h-11 md:h-8"
                :disabled="saving"
                @click="save">
                <LoaderCircle
                    v-if="saving"
                    class="animate-spin"
                    aria-hidden="true" />
                Save
            </Button>
        </div>

        <div
            v-if="changed"
            role="status"
            class="flex flex-wrap items-center gap-2 rounded-md border border-gold/50 bg-gold/10 px-3 py-2 text-sm">
            <span class="flex-1">This article changed while you were editing.</span>
            <Button
                variant="outline"
                size="sm"
                class="h-11 md:h-8"
                @click="reloadKeepingText">
                Reload
            </Button>
        </div>

        <p
            v-if="blocks.length === 0"
            class="text-sm text-muted-foreground">
            Nothing written yet. Add a block below.
        </p>
        <WikiArticleBlockEditor
            v-for="(block, index) in blocks"
            :key="block.key"
            :ref="(el) => setBlockRef(block.key, el)"
            :campaignId="campaignId"
            :block="block"
            :index="index"
            :count="blocks.length"
            :viewer="viewer"
            :canChangeVisibility="canChangeBlockVisibility(block, viewer)"
            :active="activeKey === block.key"
            :nameOf="nameOf"
            @focus="onFocus(block.key)"
            @blur="onBlur"
            @move="(delta) => move(index, delta)"
            @remove="remove(index)"
            @secret="(range) => secret(index, range)"
            @visibility="(v) => (blocks = setBlockVisibility(blocks, index, v, viewer))"
            @save="save" />

        <div class="flex flex-wrap gap-2">
            <Button
                variant="outline"
                class="h-11 gap-1 md:h-8"
                @click="add('text')">
                <Plus
                    class="size-4"
                    aria-hidden="true" />
                Text
            </Button>
            <Button
                variant="outline"
                class="h-11 gap-1 md:h-8"
                @click="add('secret')">
                <Plus
                    class="size-4"
                    aria-hidden="true" />
                🔒 Secret
            </Button>
        </div>

        <WikiConflictDialog
            v-model:open="conflictOpen"
            :items="conflictItems"
            @reload="reload" />
        <ComposerRevealDialog
            ref="reveal"
            :campaignId="campaignId"
            verb="save" />
    </section>
</template>

<script setup lang="ts">
    import { useQueryClient } from "@tanstack/vue-query";
    import { useMediaQuery } from "@vueuse/core";
    import { LoaderCircle, Plus } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { ComponentPublicInstance } from "vue";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import type { Article, Entry, Visibility } from "~/utils/api/types";
    import {
        articleRevealCheck,
        articleSaveBody,
        articleUnchanged,
        canChangeBlockVisibility,
        editedBlocks,
        moveBlock,
        newBlock,
        relinkArticleNewEntry,
        removeBlock,
        setBlockVisibility,
        toEditorBlocks,
        wrapSecret,
        type EditorBlock,
        type TextRange,
    } from "~/utils/article";
    import type { EntryViewer } from "~/utils/entries";
    import { newEntryErrorFrom, type RevealItem } from "~/utils/mentions";
    import {
        getEntriesQueryKey,
        getEntryQueryKey,
        invalidateEntries,
        putEntryArticleMutation,
        useEntryDirectory,
    } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        entry: Entry;
        viewer: EntryViewer;
        nameOf: (memberId: string) => string;
        /** A block to open at: the new quote after promoting on a phone (§3a). */
        focusBlockId?: string;
    }>();
    const emit = defineEmits<{ done: [] }>();

    let keys = 0;
    const newKey = () => `block-${++keys}`;

    // The article as loaded: the etag the save sends, and what "changed" is measured from.
    // eslint-disable-next-line vue/no-setup-props-reactivity-loss -- a snapshot on purpose
    const base = shallowRef<Article>(props.entry.article);
    const blocks = ref<EditorBlock[]>(toEditorBlocks(base.value.blocks, newKey));

    // ── Focus and the docked toolbar ─────────────────────────────────────────
    const activeKey = ref<string | null>(null);
    let blurTimer: ReturnType<typeof setTimeout> | undefined;
    function onFocus(key: string) {
        clearTimeout(blurTimer);
        activeKey.value = key;
    }
    function onBlur() {
        clearTimeout(blurTimer);
        // A grace period, so a tap on the toolbar or another block does not flicker.
        blurTimer = setTimeout(() => (activeKey.value = null), 120);
    }
    // On a phone the docked toolbar takes the tab bar's place (invariant 11).
    const phone = useMediaQuery("(max-width: 767.98px)");
    const pinnedState = useComposerPinned();
    watch(
        () => phone.value && activeKey.value !== null,
        (pinned) => (pinnedState.value = pinned)
    );
    onBeforeUnmount(() => {
        clearTimeout(blurTimer);
        pinnedState.value = false;
    });

    const blockRefs = new Map<string, { focus: () => void }>();
    function setBlockRef(key: string, el: Element | ComponentPublicInstance | null) {
        if (el) blockRefs.set(key, el as unknown as { focus: () => void });
        else blockRefs.delete(key);
    }
    function focusBlock(key: string | undefined) {
        if (!key) return;
        void nextTick(() => blockRefs.get(key)?.focus());
    }
    onMounted(() => {
        const id = props.focusBlockId?.toLowerCase();
        focusBlock(blocks.value.find((b) => id && b.id?.toLowerCase() === id)?.key);
    });

    // ── Editing ──────────────────────────────────────────────────────────────
    function move(index: number, delta: -1 | 1) {
        blocks.value = moveBlock(blocks.value, index, delta);
    }
    function remove(index: number) {
        blocks.value = removeBlock(blocks.value, index);
    }
    function add(kind: "text" | "secret") {
        const block = newBlock(kind, props.viewer, newKey);
        blocks.value = [...blocks.value, block];
        focusBlock(block.key);
    }
    function secret(index: number, range: TextRange) {
        const result = wrapSecret(blocks.value, index, range, props.viewer, newKey);
        if (!result) return;
        blocks.value = result.blocks;
        focusBlock(result.blocks[result.focusIndex]?.key);
    }

    const edited = computed(() => editedBlocks(base.value.blocks, blocks.value));
    const dirty = computed(
        () =>
            !articleUnchanged(base.value.blocks, articleSaveBody(base.value.etag, blocks.value)) ||
            edited.value.length > 0
    );
    const confirmDiscard = ref(false);
    let discardTimer: ReturnType<typeof setTimeout> | undefined;
    function cancel() {
        if (!dirty.value || confirmDiscard.value) {
            emit("done");
            return;
        }
        confirmDiscard.value = true;
        clearTimeout(discardTimer);
        discardTimer = setTimeout(() => (confirmDiscard.value = false), 4000);
    }
    onBeforeUnmount(() => clearTimeout(discardTimer));

    // ── Someone else's change ────────────────────────────────────────────────
    const saving = ref(false);
    const changed = ref(false);
    // `entryArticleChanged` reads the entry again; the editor keeps its own copy.
    watch(
        () => props.entry.article.etag,
        (etag) => {
            if (!saving.value && etag !== base.value.etag) changed.value = true;
        }
    );

    const conflictOpen = ref(false);
    const conflictItems = ref<ReturnType<typeof editedBlocks>>([]);
    const queryClient = useQueryClient();

    /** Start again from the article as it is now. */
    async function reload() {
        const key = getEntryQueryKey(props.campaignId, props.entry.id);
        await queryClient.refetchQueries({ queryKey: key, exact: true }).catch(() => {});
        const fresh = queryClient.getQueryData<Entry>(key) ?? props.entry;
        base.value = fresh.article;
        blocks.value = toEditorBlocks(fresh.article.blocks, newKey);
        changed.value = false;
    }
    /** The banner's Reload: the viewer's own text first, when they changed any. */
    function reloadKeepingText() {
        conflictItems.value = edited.value;
        if (conflictItems.value.length > 0) conflictOpen.value = true;
        else void reload();
    }

    // ── Saving ───────────────────────────────────────────────────────────────
    const directory = useEntryDirectory(() => props.campaignId);
    const reveal = useTemplateRef<{
        confirm: (v: Visibility, items: RevealItem[]) => Promise<boolean>;
    }>("reveal");
    const putArticle = putEntryArticleMutation();

    const errorKey = (error: unknown, key: string) =>
        !!(
            error as {
                response?: { data?: { errors?: Record<string, unknown> } };
            }
        )?.response?.data?.errors?.[key];

    async function save() {
        if (saving.value) return;
        const body = articleSaveBody(base.value.etag, blocks.value);
        if (articleUnchanged(base.value.blocks, body)) {
            emit("done");
            return;
        }
        const warn = articleRevealCheck(props.entry.visibility, body.blocks, directory.value, props.viewer);
        if (warn.items.length > 0 && !(await reveal.value?.confirm(warn.audience, warn.items))) return;

        saving.value = true;
        try {
            const saved = await putArticle.mutateAsync({
                campaignId: props.campaignId,
                entryId: props.entry.id,
                etag: body.etag,
                blocks: body.blocks,
                ...(body.newEntries.length > 0 ? { newEntries: body.newEntries } : {}),
            });
            // The response is the viewer's new view: the push that follows is not a change.
            base.value = saved.article;
            emit("done");
        } catch (error) {
            const status = apiErrorStatus(error);
            // A stale view: another save changed what this viewer sees (409), or a block
            // they had was removed or hidden meanwhile (400 `errors.blocks`).
            if ((status === 409 && errorKey(error, "etag")) || (status === 400 && errorKey(error, "blocks"))) {
                conflictItems.value = edited.value;
                conflictOpen.value = true;
                return;
            }
            const entryError = newEntryErrorFrom(error);
            if (entryError?.kind === "alreadyCreated") {
                // A retry after a timeout: the first save went through.
                toast.info("That save already went through.");
                void invalidateEntries(queryClient, props.campaignId);
                emit("done");
                return;
            }
            if (entryError?.kind === "duplicate") {
                const name = blocks.value
                    .flatMap((b) => b.mention.newEntries)
                    .find((e) => e.id.toLowerCase() === entryError.newEntryId.toLowerCase())?.name;
                blocks.value = relinkArticleNewEntry(blocks.value, entryError.newEntryId, entryError.existingEntryId);
                void queryClient.invalidateQueries({
                    queryKey: getEntriesQueryKey(props.campaignId),
                });
                toast.error(`"${name ?? "That entry"}" already exists, so the mention now links to it. Save again.`);
                return;
            }
            if (status === 403) {
                toast.error(
                    apiErrorMessage(
                        error,
                        "Only a block's owner or a DM can change who sees it, and only editors can save."
                    )
                );
                return;
            }
            toast.error(apiErrorMessage(error, "Could not save the article."));
        } finally {
            saving.value = false;
        }
    }
</script>
