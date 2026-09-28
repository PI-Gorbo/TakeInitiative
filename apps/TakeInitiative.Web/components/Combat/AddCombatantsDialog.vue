<template>
    <!-- Add combatants (18c.5, DMs). One box with 15d's matching: `goblin ×4`, `goblin x4`
         or `@Goblin ×4` picks Goblin four times, and a name no entry has is added without
         one. Picks collect in a staged list with the defaults the server will use, and one
         request adds them all. The dialog stays open for the rest of the encounter. On a
         phone it is a full-height sheet that ends at the keyboard, so the suggestions
         under the box and the Add button stay in view. -->
    <DialogRoot v-model:open="open">
        <DialogPortal>
            <DialogOverlay
                class="fixed inset-0 z-50 hidden bg-black/80 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 md:block" />
            <DialogContent
                class="fixed inset-x-0 top-0 z-50 flex flex-col bg-background pt-safe px-safe data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 max-md:bottom-[var(--keyboard-inset)] md:inset-x-4 md:top-[8vh] md:mx-auto md:max-h-[84vh] md:max-w-2xl md:rounded-lg md:border md:shadow-lg"
                :style="{ '--keyboard-inset': `${keyboardInset}px` }"
                @openAutoFocus.prevent="input?.focus()">
                <div
                    class="flex shrink-0 items-center gap-2 border-b py-1 pl-4 pr-1">
                    <DialogTitle class="flex-1 text-base font-semibold"
                        >Add combatants</DialogTitle
                    >
                    <DialogDescription class="sr-only">
                        Type an entry's name and a count, like goblin ×4, or a
                        name with no entry.
                    </DialogDescription>
                    <DialogClose
                        class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        aria-label="Close">
                        <X class="size-5" />
                    </DialogClose>
                </div>

                <div class="flex shrink-0 flex-col gap-1 border-b px-3 py-2">
                    <input
                        ref="input"
                        v-model="text"
                        type="text"
                        enterkeyhint="enter"
                        placeholder="goblin ×4, @Klarg, Bandit…"
                        autocomplete="off"
                        autocapitalize="off"
                        spellcheck="false"
                        role="combobox"
                        aria-label="Find an entry, with an optional count"
                        aria-autocomplete="list"
                        :aria-expanded="options.length > 0"
                        :aria-controls="`${id}-options`"
                        :aria-activedescendant="
                            options[active]
                                ? `${id}-option-${active}`
                                : undefined
                        "
                        class="h-11 w-full rounded-md border bg-background px-3 text-base outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-9 md:text-sm"
                        @keydown="onKeydown" />
                    <ul
                        :id="`${id}-options`"
                        role="listbox"
                        aria-label="Entries"
                        class="flex max-h-48 min-w-0 flex-col overflow-y-auto">
                        <li
                            v-for="(option, i) in options"
                            :id="`${id}-option-${i}`"
                            :key="
                                option.kind === 'entry'
                                    ? option.entry.id
                                    : 'plain'
                            "
                            role="option"
                            :aria-selected="i === active">
                            <button
                                type="button"
                                tabindex="-1"
                                :class="[
                                    'flex min-h-11 w-full min-w-0 items-center gap-2 rounded-md px-2 text-left text-sm md:min-h-9',
                                    i === active
                                        ? 'bg-accent'
                                        : 'hover:bg-accent/60',
                                ]"
                                @mousedown.prevent
                                @click="pick(option)">
                                <template v-if="option.kind === 'entry'">
                                    <span
                                        class="shrink-0"
                                        aria-hidden="true"
                                        >{{
                                            ENTRY_KIND_ICONS[option.entry.kind]
                                        }}</span
                                    >
                                    <span
                                        class="min-w-0 truncate font-medium"
                                        >{{ option.entry.name }}</span
                                    >
                                    <span
                                        v-if="option.alias"
                                        class="min-w-0 truncate text-xs text-muted-foreground"
                                        >· aka {{ option.alias }}</span
                                    >
                                    <span
                                        v-if="
                                            option.entry.visibility !==
                                            'Everyone'
                                        "
                                        class="shrink-0 text-xs text-muted-foreground"
                                        >🔒 starts hidden</span
                                    >
                                </template>
                                <template v-else>
                                    <Plus
                                        class="size-4 shrink-0"
                                        aria-hidden="true" />
                                    <span class="min-w-0 truncate"
                                        >Add "{{ option.name }}" without an
                                        entry</span
                                    >
                                </template>
                                <span
                                    v-if="parsed.count > 1"
                                    class="ml-auto shrink-0 text-xs font-semibold text-gold"
                                    >×{{ parsed.count }}</span
                                >
                            </button>
                        </li>
                    </ul>
                </div>

                <div class="min-h-0 flex-1 overflow-y-auto px-3 py-2">
                    <p
                        v-if="staged.length === 0"
                        class="py-6 text-center text-sm text-muted-foreground">
                        Pick entries above. They wait here until you add them.
                    </p>
                    <ul
                        v-else
                        class="flex flex-col gap-2"
                        aria-label="To add">
                        <li
                            v-for="row in staged"
                            :key="row.key"
                            class="flex flex-col gap-2 rounded-md border p-2">
                            <div class="flex min-w-0 items-center gap-2">
                                <span
                                    class="min-w-0 flex-1 truncate font-medium">
                                    {{ row.name }}
                                    <span
                                        v-if="!row.entryId"
                                        class="text-xs font-normal text-muted-foreground"
                                        >· no entry</span
                                    >
                                </span>
                                <div
                                    class="flex shrink-0 items-center rounded-md border"
                                    role="group"
                                    :aria-label="`How many of ${row.name}`">
                                    <button
                                        type="button"
                                        class="flex size-11 items-center justify-center hover:bg-accent disabled:opacity-40 md:size-8"
                                        :disabled="row.count <= 1"
                                        :aria-label="`One fewer ${row.name}`"
                                        @click="
                                            setCount(row.key, row.count - 1)
                                        ">
                                        <Minus class="size-4" />
                                    </button>
                                    <span
                                        class="w-8 text-center text-sm tabular-nums"
                                        aria-live="polite"
                                        >×{{ row.count }}</span
                                    >
                                    <button
                                        type="button"
                                        class="flex size-11 items-center justify-center hover:bg-accent disabled:opacity-40 md:size-8"
                                        :disabled="
                                            row.count >= COMBATANT_COUNT_MAX
                                        "
                                        :aria-label="`One more ${row.name}`"
                                        @click="
                                            setCount(row.key, row.count + 1)
                                        ">
                                        <Plus class="size-4" />
                                    </button>
                                </div>
                                <button
                                    type="button"
                                    class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground md:size-8"
                                    :aria-label="`Remove ${row.name}`"
                                    @click="unstage(row.key)">
                                    <X class="size-4" />
                                </button>
                            </div>
                            <div class="grid grid-cols-3 gap-2">
                                <label
                                    class="flex min-w-0 flex-col gap-1 text-xs text-muted-foreground">
                                    HP
                                    <input
                                        v-model="row.maxHp"
                                        type="text"
                                        :maxlength="COMBAT_EXPRESSION_MAX"
                                        autocomplete="off"
                                        autocapitalize="off"
                                        spellcheck="false"
                                        placeholder="2d6+2"
                                        class="h-11 w-full min-w-0 rounded-md border bg-background px-2 font-mono text-base text-foreground outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-8 md:text-sm" />
                                </label>
                                <label
                                    class="flex min-w-0 flex-col gap-1 text-xs text-muted-foreground">
                                    AC
                                    <input
                                        v-model="row.ac"
                                        type="text"
                                        inputmode="numeric"
                                        maxlength="2"
                                        autocomplete="off"
                                        placeholder="–"
                                        class="h-11 w-full min-w-0 rounded-md border bg-background px-2 text-base text-foreground outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-8 md:text-sm" />
                                </label>
                                <label
                                    class="flex min-w-0 flex-col gap-1 text-xs text-muted-foreground">
                                    Initiative
                                    <input
                                        v-model="row.initiativeRoll"
                                        type="text"
                                        :maxlength="COMBAT_EXPRESSION_MAX"
                                        autocomplete="off"
                                        autocapitalize="off"
                                        spellcheck="false"
                                        placeholder="1d20"
                                        class="h-11 w-full min-w-0 rounded-md border bg-background px-2 font-mono text-base text-foreground outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-8 md:text-sm" />
                                </label>
                            </div>
                            <p
                                v-if="row.entryId && row.stats === 'none'"
                                class="text-xs text-muted-foreground">
                                No stats: HP and AC can be set later.
                            </p>
                            <p
                                v-else-if="row.stats === 'loading'"
                                class="text-xs text-muted-foreground">
                                Reading its stats…
                            </p>
                        </li>
                    </ul>
                </div>

                <div
                    class="flex shrink-0 flex-col gap-2 border-t px-3 py-2 max-md:pb-[max(0.5rem,env(safe-area-inset-bottom))]">
                    <p
                        v-if="error"
                        class="text-sm text-destructive-tint"
                        role="alert">
                        {{ error }}
                    </p>
                    <div class="flex items-center justify-end gap-2">
                        <Button
                            type="button"
                            variant="ghost"
                            class="h-11 md:h-9"
                            @click="open = false">
                            Done
                        </Button>
                        <Button
                            type="button"
                            class="h-11 md:h-9"
                            :disabled="total === 0 || add.isPending.value"
                            @click="submit">
                            {{
                                total === 1
                                    ? "Add 1 combatant"
                                    : `Add ${total} combatants`
                            }}
                        </Button>
                    </div>
                </div>
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { useQueryClient } from "@tanstack/vue-query";
    import { Minus, Plus, X } from "lucide-vue-next";
    import {
        DialogClose,
        DialogContent,
        DialogDescription,
        DialogOverlay,
        DialogPortal,
        DialogRoot,
        DialogTitle,
    } from "reka-ui";
    import { toast } from "vue-sonner";
    import type { EntrySummary } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import {
        COMBATANT_COUNT_MAX,
        COMBAT_EXPRESSION_MAX,
        combatantRequests,
        parseAddCombatants,
        stageCombatant,
        stagedTotal,
        type StagedCombatant,
    } from "~/utils/combat";
    import { ENTRY_KIND_ICONS, entriesCalled } from "~/utils/entries";
    import { matchEntries } from "~/utils/mentions";
    import { addCombatantsMutation } from "~/utils/queries/combats";
    import { getEntryQuery, useEntryDirectory } from "~/utils/queries/entries";

    const props = defineProps<{ campaignId: string; combatId: string }>();
    const open = defineModel<boolean>("open", { required: true });

    type Option =
        | { kind: "entry"; entry: EntrySummary; alias?: string }
        | { kind: "plain"; name: string };

    const id = useId();
    const queryClient = useQueryClient();
    const keyboardInset = useKeyboardInset();
    const input = useTemplateRef<HTMLInputElement>("input");
    const directory = useEntryDirectory(() => props.campaignId);

    const text = ref("");
    const active = ref(0);
    const staged = ref<StagedCombatant[]>([]);
    const error = ref<string | null>(null);

    watch(open, (isOpen) => {
        if (!isOpen) return;
        text.value = "";
        staged.value = [];
        error.value = null;
    });

    const parsed = computed(() => parseAddCombatants(text.value));
    const options = computed<Option[]>(() => {
        if (!text.value.trim()) return [];
        const query = parsed.value.query;
        const entries: Option[] = matchEntries(query, directory.value, 6).map(
            (m) => ({
                kind: "entry",
                entry: m.entry,
                alias: m.alias,
            })
        );
        const plain: Option[] =
            query && entriesCalled(directory.value, query).length === 0
                ? [{ kind: "plain", name: query }]
                : [];
        return [...entries, ...plain];
    });
    watch(options, () => (active.value = 0));

    const total = computed(() => stagedTotal(staged.value));

    function onKeydown(event: KeyboardEvent) {
        if (event.isComposing) return;
        if (event.key === "ArrowDown" && options.value.length > 0) {
            event.preventDefault();
            active.value = (active.value + 1) % options.value.length;
        } else if (event.key === "ArrowUp" && options.value.length > 0) {
            event.preventDefault();
            active.value =
                (active.value - 1 + options.value.length) %
                options.value.length;
        } else if (event.key === "Enter") {
            event.preventDefault();
            const option = options.value[active.value];
            if (option) pick(option);
            else if (!text.value.trim() && total.value > 0) void submit();
        }
    }

    let nextKey = 0;
    function pick(option: Option) {
        const count = parsed.value.count;
        const before = staged.value.length;
        const key = `pick-${nextKey++}`;
        const isEntry = option.kind === "entry";
        staged.value = stageCombatant(
            staged.value,
            {
                entryId: isEntry ? option.entry.id : null,
                name: isEntry ? option.entry.name : option.name,
                count,
                maxHp: "",
                ac: "",
                initiativeRoll: "",
                stats: isEntry ? "loading" : "none",
            },
            key
        );
        text.value = "";
        error.value = null;
        input.value?.focus();
        // A new entry row shows the defaults the server will use: the entry's Stats.
        if (isEntry && staged.value.length > before)
            void loadStats(key, option.entry.id);
    }

    async function loadStats(key: string, entryId: string) {
        let stats:
            | {
                  maxHp?: string | null;
                  ac?: number | null;
                  initiativeRoll?: string | null;
              }
            | null
            | undefined;
        try {
            stats = (
                await queryClient.fetchQuery(
                    getEntryQuery(
                        () => props.campaignId,
                        () => entryId
                    )
                )
            ).stats;
        } catch {
            stats = null;
        }
        const row = staged.value.find((s) => s.key === key);
        if (!row) return;
        // Fill only what was not typed while the stats loaded.
        if (stats) {
            row.maxHp ||= stats.maxHp ?? "";
            row.ac ||= stats.ac != null ? String(stats.ac) : "";
            row.initiativeRoll ||= stats.initiativeRoll ?? "";
        }
        row.stats =
            stats && (stats.maxHp || stats.ac != null || stats.initiativeRoll)
                ? "some"
                : "none";
    }

    function setCount(key: string, count: number) {
        const row = staged.value.find((s) => s.key === key);
        if (row) row.count = Math.min(COMBATANT_COUNT_MAX, Math.max(1, count));
    }
    function unstage(key: string) {
        staged.value = staged.value.filter((s) => s.key !== key);
    }

    const add = addCombatantsMutation();
    async function submit() {
        const { combatants, error: invalid } = combatantRequests(staged.value);
        if (invalid) {
            error.value = invalid;
            return;
        }
        if (combatants.length === 0) return;
        try {
            await add.mutateAsync({
                campaignId: props.campaignId,
                combatId: props.combatId,
                combatants,
            });
            toast.success(
                total.value === 1
                    ? "Added 1 combatant."
                    : `Added ${total.value} combatants.`
            );
            staged.value = [];
            error.value = null;
            input.value?.focus();
        } catch (err) {
            error.value = apiErrorMessage(err, "Could not add them.");
        }
    }
</script>
