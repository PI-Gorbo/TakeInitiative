<template>
    <!-- An entry's aliases as chips (15c). Enter or a comma adds what was typed; × on
         a chip removes it. The API trims, de-duplicates and drops an alias equal to
         the name too; this only keeps the list tidy while editing. -->
    <div class="flex flex-col gap-1.5">
        <ul
            v-if="modelValue.length > 0"
            class="flex flex-wrap gap-1.5"
            aria-label="Aliases">
            <li
                v-for="alias in modelValue"
                :key="alias"
                class="flex h-9 items-center gap-1 rounded-full border pl-3 pr-1 text-sm">
                {{ alias }}
                <button
                    type="button"
                    class="flex size-7 items-center justify-center rounded-full text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                    :aria-label="`Remove alias ${alias}`"
                    @click="remove(alias)">
                    <X
                        class="size-4"
                        aria-hidden="true" />
                </button>
            </li>
        </ul>
        <div class="flex gap-2">
            <Input
                :id="inputId"
                v-model="draft"
                maxlength="100"
                autocomplete="off"
                enterkeyhint="enter"
                :disabled="full"
                :placeholder="full ? `At most ${ENTRY_ALIASES_MAX} aliases` : 'Add an alias'"
                class="h-11 text-base md:h-9 md:text-sm"
                @keydown.enter.prevent="add"
                @keydown="onComma" />
            <Button
                type="button"
                variant="secondary"
                class="h-11 shrink-0 md:h-9"
                :disabled="!draft.trim() || full"
                @click="add">
                Add
            </Button>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { X } from "lucide-vue-next";
    import { ENTRY_ALIASES_MAX, addAlias } from "~/utils/entries";

    const props = defineProps<{ name: string; inputId?: string }>();
    const modelValue = defineModel<string[]>({ required: true });

    const draft = ref("");
    const full = computed(() => modelValue.value.length >= ENTRY_ALIASES_MAX);

    function add() {
        modelValue.value = addAlias(modelValue.value, draft.value, props.name);
        draft.value = "";
    }
    function onComma(event: KeyboardEvent) {
        if (event.key !== ",") return;
        event.preventDefault();
        add();
    }
    function remove(alias: string) {
        modelValue.value = modelValue.value.filter((a) => a !== alias);
    }
</script>
