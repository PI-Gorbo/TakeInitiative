<template>
    <!-- Conditions (18d.3, §1: a text label with an optional note). Chips with a remove
         ×, and ＋ Condition: the 5e list plus Concentrating, or any text, with a note. -->
    <section
        aria-labelledby="conditions-heading"
        class="flex flex-col gap-2">
        <h3
            id="conditions-heading"
            class="text-xs font-medium text-muted-foreground">
            Conditions
        </h3>
        <ul
            v-if="conditions.length > 0"
            class="flex flex-wrap gap-1.5">
            <li
                v-for="(condition, i) in conditions"
                :key="`${condition.label}-${i}`"
                class="flex min-h-11 max-w-full items-center rounded-full bg-accent pl-3 text-sm md:min-h-8">
                <span class="min-w-0 truncate">
                    {{ condition.label
                    }}<span
                        v-if="condition.note"
                        class="text-muted-foreground">
                        · {{ condition.note }}</span
                    >
                </span>
                <button
                    type="button"
                    class="flex size-11 shrink-0 items-center justify-center rounded-full text-muted-foreground hover:text-foreground md:size-8"
                    :aria-label="`Remove ${condition.label}`"
                    :disabled="disabled"
                    @click="conditions = removeCondition(conditions, i)">
                    <X
                        class="size-4"
                        aria-hidden="true" />
                </button>
            </li>
        </ul>

        <Button
            v-if="!adding"
            variant="outline"
            class="h-11 w-fit gap-2 md:h-9"
            :disabled="disabled || conditions.length >= CONDITIONS_MAX"
            @click="startAdding">
            <Plus
                class="size-4"
                aria-hidden="true" />
            Condition
        </Button>
        <div
            v-else
            class="flex flex-col gap-2 rounded-md border p-2">
            <div class="flex gap-2">
                <input
                    ref="labelInput"
                    v-model="label"
                    type="text"
                    enterkeyhint="done"
                    autocomplete="off"
                    :maxlength="CONDITION_LABEL_MAX"
                    placeholder="Poisoned, Hexed…"
                    aria-label="Condition"
                    class="h-11 min-w-0 flex-1 rounded-md border bg-background px-3 text-base outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-9 md:text-sm"
                    @keydown.enter.prevent="add(label)"
                    @keydown.esc.stop.prevent="adding = false" />
                <Button
                    class="h-11 md:h-9"
                    :disabled="!label.trim()"
                    @click="add(label)">
                    Add
                </Button>
                <Button
                    variant="ghost"
                    class="h-11 md:h-9"
                    @click="adding = false">
                    Cancel
                </Button>
            </div>
            <input
                v-model="note"
                type="text"
                enterkeyhint="done"
                autocomplete="off"
                :maxlength="CONDITION_NOTE_MAX"
                placeholder="Note (optional): until the end of its next turn…"
                aria-label="Note"
                class="h-11 w-full rounded-md border bg-background px-3 text-base outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-9 md:text-sm"
                @keydown.enter.prevent="add(label)" />
            <div
                v-if="suggestions.length > 0"
                class="flex flex-wrap gap-1.5"
                role="group"
                aria-label="Conditions">
                <button
                    v-for="suggestion in suggestions"
                    :key="suggestion"
                    type="button"
                    class="flex min-h-11 items-center rounded-full border px-3 text-sm hover:bg-accent md:min-h-8"
                    @click="add(suggestion)">
                    {{ suggestion }}
                </button>
            </div>
            <p
                v-if="error"
                class="text-xs text-destructive-tint">
                {{ error }}
            </p>
        </div>
    </section>
</template>

<script setup lang="ts">
    import { Plus, X } from "lucide-vue-next";
    import type { CombatCondition } from "~/utils/api/types";
    import {
        CONDITIONS_MAX,
        CONDITION_LABEL_MAX,
        CONDITION_NOTE_MAX,
        addCondition,
        conditionSuggestions,
        removeCondition,
    } from "~/utils/combat";

    defineProps<{ disabled?: boolean }>();
    const conditions = defineModel<CombatCondition[]>({ required: true });

    const adding = ref(false);
    const label = ref("");
    const note = ref("");
    const error = ref<string | null>(null);
    const labelInput = ref<HTMLInputElement | null>(null);

    const suggestions = computed(() =>
        conditionSuggestions(label.value, conditions.value)
    );

    async function startAdding() {
        adding.value = true;
        label.value = "";
        note.value = "";
        error.value = null;
        // On a phone the list is one tap away; the keyboard waits for a tap on the box.
        if (!window.matchMedia?.("(pointer: fine)").matches) return;
        await nextTick();
        labelInput.value?.focus();
    }

    function add(text: string) {
        const result = addCondition(conditions.value, text, note.value);
        if (result.error) {
            error.value = result.error;
            return;
        }
        conditions.value = result.conditions;
        adding.value = false;
    }
</script>
