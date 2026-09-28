<template>
    <!-- The viewer's loose ends, each resolved in place (19e). A resolved row stays with
         ✓ for a moment after the counts refetch, then leaves. -->
    <ul class="flex flex-col gap-2">
        <li
            v-for="item in shown"
            :key="looseEndKey(item)">
            <LooseEndsLooseEndRow
                :campaignId="campaignId"
                :item="item"
                :authorName="authorName"
                :resolved="resolvedKeys.has(looseEndKey(item))"
                :highlighted="!!item.note && item.note.id === highlightedNoteId"
                @resolved="hold(item)"
                @openImage="(imageId) => emit('openImage', imageId)" />
        </li>
    </ul>
</template>

<script setup lang="ts">
    import { useQueryClient } from "@tanstack/vue-query";
    import type { LooseEnd } from "~/utils/api/types";
    import { looseEndKey, withHeld, type HeldLooseEnd } from "~/utils/looseEnds";
    import { invalidateLooseEnds } from "~/utils/queries/looseEnds";

    /** How long a resolved row shows its ✓. */
    const HOLD_MS = 1500;

    const props = defineProps<{
        campaignId: string;
        items: readonly LooseEnd[];
        authorName: string;
        highlightedNoteId?: string | null;
    }>();
    const emit = defineEmits<{ openImage: [imageId: string] }>();

    const queryClient = useQueryClient();
    const held = ref<HeldLooseEnd[]>([]);
    const shown = computed(() => withHeld(props.items, held.value));
    const resolvedKeys = computed(() => new Set(held.value.map((h) => looseEndKey(h.item))));

    const timers = new Set<ReturnType<typeof setTimeout>>();
    function hold(item: LooseEnd) {
        const key = looseEndKey(item);
        const index = shown.value.findIndex((i) => looseEndKey(i) === key);
        held.value = [...held.value, { item, index: Math.max(0, index) }];
        // The counts (dividers, the Wiki, ⌘K) drop now; the row waits for its ✓.
        void invalidateLooseEnds(queryClient, props.campaignId);
        const timer = setTimeout(() => {
            timers.delete(timer);
            held.value = held.value.filter((h) => looseEndKey(h.item) !== key);
        }, HOLD_MS);
        timers.add(timer);
    }
    onBeforeUnmount(() => timers.forEach(clearTimeout));

    /** Whether a resolved row is still showing its ✓ (the page waits for it before "Nothing loose"). */
    defineExpose({ holding: computed(() => held.value.length > 0) });
</script>
