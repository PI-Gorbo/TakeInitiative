<template>
    <!-- The list's shape while the first page loads (SAM-29): the same count line and the same
         row box, so the page does not grow under the reader when the rows arrive. -->
    <div
        class="flex w-full flex-col gap-3"
        role="status">
        <span class="sr-only">Loading reference material</span>
        <Skeleton class="h-5 w-40" />
        <ul class="flex flex-col gap-2">
            <li
                v-for="row in ROWS"
                :key="row">
                <!-- `KnowledgeBaseRow`'s box: `p-3`, a `size-11` artwork slot and three lines. -->
                <div class="flex items-start gap-3 rounded-lg border bg-card p-3">
                    <Skeleton class="size-11 shrink-0 rounded-md" />
                    <div class="flex min-w-0 flex-1 flex-col">
                        <div class="flex h-6 items-center">
                            <Skeleton class="h-4 w-2/5" />
                        </div>
                        <div class="flex h-5 items-center">
                            <Skeleton class="h-3 w-3/5" />
                        </div>
                        <div class="flex h-4 items-center">
                            <Skeleton class="h-2.5 w-1/4" />
                        </div>
                    </div>
                </div>
            </li>
        </ul>
    </div>
</template>

<script setup lang="ts">
    // Enough rows to fill a laptop viewport; a real page is `KNOWLEDGE_BASE_PAGE_SIZE` of them.
    const ROWS = 8;
</script>
