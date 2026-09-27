<template>
    <!-- An entry's history (15g, design §4: "every change is an event with an actor, so
         history is viewable and changes can be reverted"). The server redacts it for the
         viewer: an article version lists only the blocks they can see, and an edit
         inside a secret they cannot see is not there at all. "Restore this version" opens
         the article editor on that version; saving it keeps the blocks the viewer cannot
         see, in place. -->
    <Dialog v-model:open="open">
        <DialogContent class="max-h-[90dvh] max-w-lg overflow-y-auto max-md:top-4 max-md:translate-y-0">
            <DialogHeader>
                <DialogTitle>History of {{ entryName }}</DialogTitle>
                <DialogDescription>Every change you can see, newest first.</DialogDescription>
            </DialogHeader>

            <LoadingFallback
                v-if="!history"
                :isLoading="query.isLoading.value"
                :isError="query.isError.value" />
            <ol
                v-else
                class="flex flex-col gap-3">
                <li
                    v-for="{ item, index } in newestFirst"
                    :key="index"
                    class="flex flex-col gap-1 border-b pb-3 last:border-b-0">
                    <p class="text-sm">
                        <span class="font-medium">{{ nameOf(item.actorMemberId) }}</span>
                        {{ describeChange(item.change, nameOf) }}
                    </p>
                    <p class="text-xs text-muted-foreground">
                        <time :datetime="item.at">{{ formatAt(item.at) }}</time>
                        <span v-if="index === current"> · current article</span>
                    </p>
                    <details
                        v-if="isArticleVersion(item.change)"
                        class="rounded-md border px-3 py-2">
                        <summary class="cursor-pointer text-sm text-muted-foreground">This version</summary>
                        <div class="pt-2">
                            <WikiArticle
                                :campaignId="campaignId"
                                :article="{ etag: '', blocks: item.change.blocks ?? [] }"
                                :viewerMemberId="viewerMemberId"
                                :canEdit="false"
                                :nameOf="nameOf" />
                            <Button
                                v-if="canEdit && index !== current"
                                variant="outline"
                                class="mt-2 h-11 md:h-8"
                                @click="restore(item)">
                                Restore this version
                            </Button>
                        </div>
                    </details>
                </li>
            </ol>
        </DialogContent>
    </Dialog>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import type { ArticleBlock, EntryHistoryItem } from "~/utils/api/types";
    import { currentVersionIndex, describeChange, isArticleVersion } from "~/utils/entries";
    import { getEntryHistoryQuery } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        entryId: string;
        entryName: string;
        viewerMemberId: string;
        canEdit: boolean;
        nameOf: (memberId: string) => string;
    }>();
    const open = defineModel<boolean>("open", { required: true });
    const emit = defineEmits<{ restore: [version: { blocks: ArticleBlock[]; label: string }] }>();

    const query = useQuery(
        getEntryHistoryQuery(
            () => props.campaignId,
            () => props.entryId,
            () => open.value
        )
    );
    const history = computed(() => query.data.value);
    const current = computed(() => (history.value ? currentVersionIndex(history.value.items) : -1));
    const newestFirst = computed(() =>
        (history.value?.items ?? []).map((item, index) => ({ item, index })).reverse()
    );

    const formatAt = (at: string) =>
        new Date(at).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });

    function restore(item: EntryHistoryItem) {
        emit("restore", { blocks: item.change.blocks ?? [], label: formatAt(item.at) });
        open.value = false;
    }
</script>
