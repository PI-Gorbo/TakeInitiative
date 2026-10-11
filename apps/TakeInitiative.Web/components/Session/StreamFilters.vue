<template>
    <!-- The stream's filter (14e, glossary §1; a dropdown since SAM-30, where the old chip
         row read as a second tab bar under the header). -->
    <DropdownMenu>
        <DropdownMenuTrigger
            class="flex h-11 min-w-0 items-center gap-1 rounded-md px-2 text-sm hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-8"
            :aria-label="`Filter: ${label}. Change filter`">
            <ListFilter
                class="size-5 shrink-0 text-muted-foreground sm:hidden"
                aria-hidden="true" />
            <span class="hidden text-muted-foreground sm:inline">Filter:</span>
            <span :class="['truncate font-medium', modelValue !== 'All' && 'text-gold']">{{ label }}</span>
            <ChevronDown
                class="size-4 shrink-0 text-muted-foreground"
                aria-hidden="true" />
        </DropdownMenuTrigger>
        <DropdownMenuContent
            align="start"
            class="w-56">
            <DropdownMenuLabel>Filter</DropdownMenuLabel>
            <DropdownMenuRadioGroup
                :modelValue="modelValue"
                @update:modelValue="(v) => (modelValue = v as SessionStreamFilter)">
                <DropdownMenuRadioItem
                    v-for="option in STREAM_FILTERS"
                    :key="option.value"
                    :value="option.value"
                    class="min-h-11 md:min-h-8">
                    {{ option.label }}
                </DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
        </DropdownMenuContent>
    </DropdownMenu>
</template>

<script setup lang="ts">
    import { ChevronDown, ListFilter } from "lucide-vue-next";
    import type { SessionStreamFilter } from "~/utils/api/types";
    import { STREAM_FILTERS, filterLabel } from "~/utils/streamFilters";

    const modelValue = defineModel<SessionStreamFilter>({ required: true });

    const label = computed(() => filterLabel(modelValue.value));
</script>
