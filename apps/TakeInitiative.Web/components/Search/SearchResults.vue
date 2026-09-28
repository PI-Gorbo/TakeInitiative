<template>
    <!-- ⌘K's results (17b): one listbox, a group per section under a sticky header.
         The cursor is the input's `aria-activedescendant`; hovering moves it. -->
    <div
        :id="listboxId"
        role="listbox"
        aria-label="Search results"
        class="flex flex-col">
        <div
            v-for="group in groups"
            :key="group.header.id"
            role="group"
            :aria-labelledby="optionId(group.header.id)">
            <div
                :id="optionId(group.header.id)"
                role="presentation"
                class="sticky top-0 z-10 bg-background/95 px-4 pb-1 pt-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground backdrop-blur">
                {{ group.header.label }}
            </div>
            <div
                v-for="{ row, index } in group.rows"
                :id="optionId(row.id)"
                :key="row.id"
                role="option"
                :aria-selected="index === cursor"
                :class="[
                    'mx-2 flex min-h-11 cursor-pointer items-center rounded-md px-2 py-2',
                    index === cursor && 'bg-accent text-accent-foreground',
                ]"
                @pointermove="cursor !== index && emit('update:cursor', index)"
                @click="emit('choose', row)">
                <SearchHitRow
                    v-if="row.type === 'hit'"
                    :campaignId="campaignId"
                    :hit="row.hit"
                    :section="row.section"
                    :authorName="authorName" />
                <span
                    v-else-if="row.type === 'more'"
                    class="flex items-center gap-2 pl-12 text-sm text-muted-foreground">
                    <LoaderCircle
                        v-if="loadingMore === row.section"
                        class="size-4 animate-spin"
                        aria-hidden="true" />
                    {{ row.label }}
                </span>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { LoaderCircle } from "lucide-vue-next";
    import type { SearchSectionKey } from "~/utils/api/types";
    import type { SearchRow } from "~/utils/search";

    const props = defineProps<{
        campaignId: string;
        rows: readonly SearchRow[];
        cursor: number;
        listboxId: string;
        optionId: (rowId: string) => string;
        authorName: (memberId: string) => string;
        loadingMore?: SearchSectionKey | null;
    }>();
    const emit = defineEmits<{ "update:cursor": [index: number]; choose: [row: SearchRow] }>();

    type Selectable = Exclude<SearchRow, { type: "header" }>;
    const groups = computed(() => {
        const result: { header: Extract<SearchRow, { type: "header" }>; rows: { row: Selectable; index: number }[] }[] = [];
        props.rows.forEach((row, index) => {
            if (row.type === "header") result.push({ header: row, rows: [] });
            else result[result.length - 1]?.rows.push({ row, index });
        });
        return result;
    });
</script>
