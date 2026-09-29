<template>
    <!-- A collapsible section of the entry page (25e): a 52px row with the title, a one-line
         peek while closed ("4 images", "AC 15 · HP 38 · Init +3") and a chevron. Its content
         mounts when it opens, so a closed Gallery draws no tiles. `forceOpen` (25f, the
         desktop's right-hand panel) keeps it open and drops the toggle. The caller draws
         any border, so sections can share one bordered group. -->
    <Collapsible
        :open="isOpen"
        :disabled="forceOpen"
        @update:open="(value: boolean) => (open = value)">
        <h3 class="text-sm">
            <span
                v-if="forceOpen"
                class="flex min-h-[52px] items-center px-4 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                <slot name="title">{{ title }}</slot>
            </span>
            <CollapsibleTrigger
                v-else
                class="flex min-h-[52px] w-full items-center gap-3 px-4 text-left hover:bg-accent/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring">
                <span class="shrink-0 font-medium">
                    <slot name="title">{{ title }}</slot>
                </span>
                <span
                    v-if="!isOpen"
                    class="min-w-0 flex-1 truncate text-muted-foreground">
                    <slot name="peek">{{ peek }}</slot>
                </span>
                <span
                    v-else
                    class="flex-1" />
                <ChevronDown
                    class="size-4 shrink-0 text-muted-foreground transition-transform"
                    :class="{ 'rotate-180': isOpen }"
                    aria-hidden="true" />
            </CollapsibleTrigger>
        </h3>
        <CollapsibleContent>
            <div class="flex flex-col gap-3 px-4 pb-4">
                <slot />
            </div>
        </CollapsibleContent>
    </Collapsible>
</template>

<script setup lang="ts">
    import { ChevronDown } from "lucide-vue-next";

    const props = withDefaults(
        defineProps<{
            title: string;
            /** The one line shown while closed; the `peek` slot replaces it. */
            peek?: string;
            defaultOpen?: boolean;
            /** Always open, with no toggle (25f's right-hand panel). */
            forceOpen?: boolean;
        }>(),
        { peek: "", defaultOpen: false, forceOpen: false }
    );

    const open = ref(props.defaultOpen);
    const isOpen = computed(() => props.forceOpen || open.value);
</script>
