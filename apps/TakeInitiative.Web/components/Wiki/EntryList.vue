<template>
    <!-- One column on a phone, two from lg (15c). -->
    <ul class="grid grid-cols-1 gap-2 lg:grid-cols-2">
        <li
            v-for="item in items"
            :key="item.entry.id">
            <WikiEntryListItem
                :campaignId="campaignId"
                :item="item" />
        </li>
    </ul>
</template>

<script setup lang="ts">
    import type { EntryListItem } from "~/utils/api/types";

    defineProps<{ campaignId: string; items: readonly EntryListItem[] }>();
</script>
