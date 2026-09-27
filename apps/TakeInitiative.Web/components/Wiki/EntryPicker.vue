<template>
    <!-- Picks the entry to promote into (15f): 15d's matching over the entries the
         viewer can edit, plus Create "…" with a kind. The new entry is created with
         the note's visibility when the dialog promotes. Merge (15g) uses it with
         `noCreate` and the merged entry left out. -->
    <div class="flex flex-col gap-2">
        <div
            v-if="target"
            class="flex min-h-11 items-center gap-2 rounded-md border px-3 py-1.5 text-sm">
            <span aria-hidden="true">{{ ENTRY_KIND_ICONS[target.kind === "entry" ? target.entry.kind : kind] }}</span>
            <span class="min-w-0 flex-1 truncate font-medium">
                {{ target.kind === "entry" ? target.entry.name : target.name }}
            </span>
            <span
                v-if="target.kind === 'create'"
                class="rounded bg-gold/15 px-1 text-xs text-gold"
                >new</span
            >
            <Button
                v-if="!locked"
                variant="ghost"
                size="sm"
                class="h-11 md:h-7"
                @click="clear">
                Change
            </Button>
        </div>
        <template v-else>
            <input
                ref="input"
                v-model="query"
                type="search"
                :aria-label="noCreate ? 'Find an entry' : 'Find or create an entry'"
                :placeholder="noCreate ? 'Find an entry…' : 'Find or create an entry…'"
                :aria-controls="`${id}-list`"
                class="h-11 w-full rounded-md border bg-background px-3 text-base outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-9 md:text-sm"
                @keydown.enter.prevent="targets[0] && choose(targets[0])" />
            <ul
                :id="`${id}-list`"
                role="listbox"
                aria-label="Entries"
                class="flex max-h-56 flex-col overflow-y-auto">
                <li
                    v-for="option in targets"
                    :key="option.kind === 'entry' ? option.entry.id : 'create'"
                    role="option"
                    :aria-selected="false">
                    <button
                        type="button"
                        class="flex min-h-11 w-full items-center gap-2 rounded-md px-2 text-left text-sm hover:bg-accent md:min-h-9"
                        @click="choose(option)">
                        <template v-if="option.kind === 'entry'">
                            <span aria-hidden="true">{{ ENTRY_KIND_ICONS[option.entry.kind] }}</span>
                            <span class="truncate font-medium">{{ option.entry.name }}</span>
                            <span
                                v-if="option.alias"
                                class="truncate text-xs text-muted-foreground"
                                >· aka {{ option.alias }}</span
                            >
                        </template>
                        <template v-else>
                            <Plus
                                class="size-4 shrink-0"
                                aria-hidden="true" />
                            <span class="truncate">Create "{{ option.name }}"</span>
                        </template>
                    </button>
                </li>
                <li
                    v-if="targets.length === 0"
                    class="px-2 py-2 text-sm text-muted-foreground">
                    {{
                        query.trim()
                            ? "No entry you can edit is called that."
                            : noCreate
                              ? "No other entries you can edit."
                              : "No entries you can edit yet. Type a name to create one."
                    }}
                </li>
            </ul>
        </template>
        <ComposerKindChips
            v-if="target?.kind === 'create'"
            :modelValue="kind"
            @pick="(k) => (kind = k)" />
    </div>
</template>

<script setup lang="ts">
    import { Plus } from "lucide-vue-next";
    import type { EntryKind } from "~/utils/api/types";
    import { ENTRY_KIND_ICONS, type EntryViewer } from "~/utils/entries";
    import { promoteTargets, type PromoteTarget } from "~/utils/promote";
    import { useEntryDirectory } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        viewer: EntryViewer;
        /** The entry is already picked (the timeline's Promote). */
        locked?: boolean;
        /** No Create "…" row (merge, 15g). */
        noCreate?: boolean;
        /** An entry to leave out: the one being merged. */
        excludeId?: string;
    }>();
    const target = defineModel<PromoteTarget | null>({ required: true });
    /** The kind of an entry to create. */
    const kind = defineModel<EntryKind>("kind", { default: "Character" });

    const id = useId();
    const query = ref("");
    const directory = useEntryDirectory(() => props.campaignId);
    const targets = computed(() =>
        promoteTargets(query.value, directory.value, props.viewer).filter((t) =>
            t.kind === "create"
                ? !props.noCreate
                : !props.excludeId || t.entry.id.toLowerCase() !== props.excludeId.toLowerCase()
        )
    );
    const input = useTemplateRef<HTMLInputElement>("input");

    function choose(option: PromoteTarget) {
        target.value = option;
    }
    function clear() {
        target.value = null;
        void nextTick(() => input.value?.focus());
    }
</script>
