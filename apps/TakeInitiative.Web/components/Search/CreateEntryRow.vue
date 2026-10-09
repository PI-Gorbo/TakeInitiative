<template>
    <!-- ⌘K's "＋ Create entry "X"" (17c): the name, a kind chip and a visibility chip.
         On desktop Tab cycles the kind and Shift+Tab the visibility, and Enter creates.
         A tap (or a click) opens the chips and Create under it, so nothing is created by
         a single tap. `mousedown.prevent` keeps the search input focused. -->
    <div class="flex min-w-0 flex-1 flex-col gap-2">
        <div class="flex min-w-0 flex-1 flex-wrap items-center gap-x-3 gap-y-1">
            <span
                class="flex size-9 shrink-0 items-center justify-center rounded-md bg-muted text-lg"
                aria-hidden="true">
                ＋
            </span>
            <span class="min-w-0 flex-1 truncate font-medium">{{ label }}</span>
            <span class="flex shrink-0 flex-wrap gap-1 text-xs">
                <span class="rounded-full border px-2 py-0.5">
                    <span aria-hidden="true">{{ ENTRY_KIND_ICONS[kind] }}</span>
                    {{ kind }}
                </span>
                <span :class="['rounded-full border px-2 py-0.5', visibility !== 'Everyone' && 'border-gold text-gold']">
                    {{ visibilityLabel }}
                </span>
            </span>
        </div>
        <span class="hidden pl-12 text-xs text-muted-foreground md:block">
            Tab: kind · Shift+Tab: visibility · Enter: create
        </span>

        <div
            v-if="expanded"
            class="flex flex-col gap-2 pl-12"
            @click.stop>
            <ComposerKindChips
                :modelValue="kind"
                class="flex-wrap"
                @pick="(k) => emit('update:kind', k)" />
            <div
                role="group"
                aria-label="Visibility of the new entry"
                class="flex flex-wrap gap-1">
                <button
                    v-for="option in ENTRY_VISIBILITY_OPTIONS"
                    :key="option.value"
                    type="button"
                    :aria-pressed="option.value === visibility"
                    :class="[
                        'flex h-11 shrink-0 items-center rounded-full border px-3 text-sm',
                        option.value === visibility ? 'border-gold bg-gold/15 text-gold' : 'hover:bg-accent/60',
                    ]"
                    @mousedown.prevent
                    @click="emit('update:visibility', option.value)">
                    {{ option.label }}
                </button>
            </div>
            <Button
                type="button"
                class="h-11 self-start md:h-9"
                :disabled="pending"
                @mousedown.prevent
                @click="emit('create')">
                <LoaderCircle
                    v-if="pending"
                    class="size-4 animate-spin"
                    aria-hidden="true" />
                Create
            </Button>
        </div>

        <p
            v-if="error"
            class="pl-12 text-sm text-destructive-tint"
            role="alert"
            @click.stop>
            {{ error }}
            <NuxtLink
                v-if="existing"
                :to="`/app/campaigns/${encodeURIComponent(campaignId)}/wiki/${encodeURIComponent(existing.id)}`"
                class="underline"
                @click="emit('openExisting')">
                Open {{ existing.name }}
            </NuxtLink>
        </p>
    </div>
</template>

<script setup lang="ts">
    import { LoaderCircle } from "lucide-vue-next";
    import type { EntryKind, EntrySummary, Visibility } from "~/utils/api/types";
    import { ENTRY_KIND_ICONS, ENTRY_VISIBILITY_OPTIONS } from "~/utils/entries";

    const props = defineProps<{
        campaignId: string;
        label: string;
        kind: EntryKind;
        visibility: Visibility;
        /** Tapped: the kind chips, the visibility chips and Create show. */
        expanded: boolean;
        pending: boolean;
        error: string | null;
        /** The visible entry a 409 named, linked from the error. */
        existing: Pick<EntrySummary, "id" | "name"> | null;
    }>();
    const emit = defineEmits<{
        "update:kind": [kind: EntryKind];
        "update:visibility": [visibility: Visibility];
        create: [];
        openExisting: [];
    }>();

    const visibilityLabel = computed(
        () => ENTRY_VISIBILITY_OPTIONS.find((o) => o.value === props.visibility)?.label ?? props.visibility
    );
</script>
