<template>
    <!-- "Visible to: Everyone ▾" (= /dm and /me in 14e). -->
    <DropdownMenu>
        <DropdownMenuTrigger
            :class="[
                'flex h-11 min-w-0 items-center gap-1 rounded-md px-2 text-sm hover:bg-accent hover:text-accent-foreground md:h-8',
                triggerClass,
            ]"
            :aria-label="`Visible to ${current.label}. Change visibility`">
            <span class="hidden text-muted-foreground sm:inline">Visible to:</span>
            <span
                v-if="modelValue === 'Everyone'"
                class="sm:hidden"
                aria-hidden="true"
                >👁</span
            >
            <span :class="['truncate font-medium', modelValue !== 'Everyone' && 'text-gold']">{{ current.label }}</span>
            <ChevronDown
                class="size-4 shrink-0 text-muted-foreground"
                aria-hidden="true" />
        </DropdownMenuTrigger>
        <DropdownMenuContent
            align="start"
            side="top"
            class="w-64">
            <DropdownMenuLabel>Visible to</DropdownMenuLabel>
            <DropdownMenuRadioGroup
                :modelValue="modelValue"
                @update:modelValue="(v) => (modelValue = v as Visibility)">
                <DropdownMenuRadioItem
                    v-for="option in VISIBILITY_OPTIONS"
                    :key="option.value"
                    :value="option.value"
                    class="min-h-11 md:min-h-8">
                    <span class="flex flex-col">
                        <span>{{ option.label }}</span>
                        <span class="text-xs text-muted-foreground">{{ option.hint }}</span>
                    </span>
                </DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
        </DropdownMenuContent>
    </DropdownMenu>
</template>

<script setup lang="ts">
    import { ChevronDown } from "lucide-vue-next";
    import type { Visibility } from "~/utils/api/types";
    import { VISIBILITY_OPTIONS } from "~/utils/composer";

    defineProps<{ triggerClass?: string }>();
    const modelValue = defineModel<Visibility>({ required: true });

    const current = computed(() => VISIBILITY_OPTIONS.find((o) => o.value === modelValue.value) ?? VISIBILITY_OPTIONS[0]);
</script>
