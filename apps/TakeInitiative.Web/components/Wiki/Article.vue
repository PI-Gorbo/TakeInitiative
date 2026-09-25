<template>
    <!-- An entry's article, read (15f, design §4): its blocks in order, as the viewer
         sees them. Secret blocks the viewer cannot see never reach the page. -->
    <section
        :aria-labelledby="`${id}-title`"
        class="flex flex-col gap-2">
        <div class="flex items-center gap-2">
            <h3
                :id="`${id}-title`"
                class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Article
            </h3>
            <div class="flex-1" />
            <Button
                v-if="canEdit && article.blocks.length > 0"
                variant="ghost"
                size="sm"
                class="h-11 gap-1 text-muted-foreground md:h-8"
                @click="emit('edit')">
                <Pencil
                    class="size-4"
                    aria-hidden="true" />
                Edit
            </Button>
        </div>
        <div
            v-if="article.blocks.length === 0"
            class="flex flex-wrap items-center gap-2 py-2 text-sm text-muted-foreground">
            <span>Nothing written yet.</span>
            <Button
                v-if="canEdit"
                variant="outline"
                size="sm"
                class="h-11 gap-1 md:h-8"
                @click="emit('edit')">
                <Pencil
                    class="size-4"
                    aria-hidden="true" />
                Edit
            </Button>
        </div>
        <div
            v-else
            class="flex flex-col gap-3">
            <WikiArticleBlock
                v-for="block in article.blocks"
                :key="block.id"
                :campaignId="campaignId"
                :block="block"
                :viewerMemberId="viewerMemberId"
                :nameOf="nameOf" />
        </div>
    </section>
</template>

<script setup lang="ts">
    import { Pencil } from "lucide-vue-next";
    import type { Article } from "~/utils/api/types";

    defineProps<{
        campaignId: string;
        article: Article;
        viewerMemberId: string;
        canEdit: boolean;
        nameOf: (memberId: string) => string;
    }>();
    const emit = defineEmits<{ edit: [] }>();
    const id = useId();
</script>
