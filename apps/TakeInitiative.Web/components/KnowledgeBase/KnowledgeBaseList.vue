<template>
    <!-- The browse list (26f): "2,847 items · 5eTools", the rows, and `Load more`.
         Not infinite scroll: a corpus of thousands of near-identical rows is the worst
         thing to scroll past by accident, and a page the reader asked for is one they can
         stop asking for. -->
    <div class="flex flex-col gap-3">
        <p
            class="text-sm text-muted-foreground"
            role="status">
            {{ countLine(total, providerLabels) }}
        </p>
        <ul class="flex flex-col gap-2">
            <KnowledgeBaseRow
                v-for="item in items"
                :key="`${item.provider}/${item.id}`"
                :item="item" />
        </ul>
        <div
            v-if="hasMore"
            class="flex justify-center">
            <Button
                variant="outline"
                class="h-11 md:h-9"
                :disabled="loadingMore"
                @click="emit('loadMore')">
                <LoaderCircle
                    v-if="loadingMore"
                    class="size-4 animate-spin"
                    aria-hidden="true" />
                Load more
            </Button>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { LoaderCircle } from "lucide-vue-next";
    import type { KnowledgeBaseItem } from "~/utils/api/types";
    import { countLine } from "~/utils/knowledgeBase";

    defineProps<{
        items: readonly KnowledgeBaseItem[];
        total: number;
        providerLabels: readonly string[];
        hasMore: boolean;
        loadingMore: boolean;
    }>();
    const emit = defineEmits<{ loadMore: [] }>();
</script>
