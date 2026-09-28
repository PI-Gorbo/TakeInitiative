<template>
    <!-- The graph's controls (19d): kind chips (several at once, all on by default, the
         last one on stays on), Depth 1 · 2 around a focus, and Clear focus. On a phone
         the chips scroll sideways, like the Wiki's kind filter. -->
    <div class="flex flex-col gap-1.5 md:flex-row md:items-center md:gap-3">
        <div
            role="group"
            aria-label="Show entries of these kinds"
            class="flex min-w-0 items-center gap-1.5 overflow-x-auto py-0.5 [scrollbar-width:none] md:flex-1">
            <button
                v-for="kind in ENTRY_KINDS"
                :key="kind.value"
                type="button"
                :aria-pressed="kinds.includes(kind.value)"
                :class="[
                    'flex h-11 shrink-0 items-center gap-1.5 rounded-full border px-3 text-sm transition-colors md:h-8',
                    kinds.includes(kind.value)
                        ? 'border-gold/60 bg-gold/15 font-medium text-gold'
                        : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
                ]"
                @click="kinds = toggleKind(kinds, kind.value)">
                <span
                    class="size-2.5 shrink-0 rounded-full"
                    :style="{ backgroundColor: KIND_COLOURS[kind.value] }"
                    aria-hidden="true" />
                <span aria-hidden="true">{{ kind.icon }}</span>
                {{ kind.label }}
            </button>
        </div>

        <div
            v-if="focusName"
            class="flex shrink-0 flex-wrap items-center gap-2">
            <span
                class="text-sm text-muted-foreground"
                aria-hidden="true"
                >Depth</span
            >
            <WikiChoiceChips
                v-model="depthChoice"
                label="Depth"
                :options="DEPTH_OPTIONS" />
            <Button
                variant="ghost"
                class="h-11 gap-1 px-2 text-sm text-muted-foreground md:h-8"
                :aria-label="`Clear focus on ${focusName}`"
                @click="emit('clearFocus')">
                <X
                    class="size-4"
                    aria-hidden="true" />
                Clear focus
            </Button>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { X } from "lucide-vue-next";
    import type { EntryKind } from "~/utils/api/types";
    import { ENTRY_KINDS } from "~/utils/entries";
    import { KIND_COLOURS, toggleKind, type GraphDepth } from "~/utils/graph";

    defineProps<{
        /** The focus's name; the depth toggle and Clear focus show only with one. */
        focusName: string | null;
    }>();
    const kinds = defineModel<EntryKind[]>("kinds", { required: true });
    const depth = defineModel<GraphDepth>("depth", { required: true });
    const emit = defineEmits<{ clearFocus: [] }>();

    const DEPTH_OPTIONS = [
        { value: "1", label: "1", hint: "Its connections" },
        { value: "2", label: "2", hint: "Their connections too" },
    ] as const;
    const depthChoice = computed<"1" | "2">({
        get: () => (depth.value === 2 ? "2" : "1"),
        set: (value) => (depth.value = value === "2" ? 2 : 1),
    });
</script>
