<template>
    <!-- Editing a note in place (author only). Enter saves on desktop, Esc cancels. -->
    <form
        class="flex flex-col gap-1 py-1"
        aria-label="Edit note"
        @submit.prevent="save">
        <textarea
            ref="textarea"
            v-model="text"
            aria-label="Note text"
            :enterkeyhint="touch ? 'enter' : 'done'"
            class="max-h-[50dvh] min-h-11 w-full resize-none overflow-y-auto rounded-md border bg-background px-3 py-2 text-base leading-snug outline-none focus-visible:ring-1 focus-visible:ring-ring md:text-sm"
            @keydown="onKeydown"
            @input="grow" />
        <div class="flex flex-wrap items-center gap-1">
            <button
                type="button"
                :aria-pressed="isRecap"
                :class="[
                    'flex h-11 items-center gap-1 rounded-md px-2 text-sm md:h-8',
                    isRecap ? 'bg-gold/15 text-gold' : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
                ]"
                @click="isRecap = !isRecap">
                <ScrollText
                    class="size-4"
                    aria-hidden="true" />
                Recap
            </button>
            <span
                v-if="overLimit"
                class="text-xs text-destructive-tint">
                {{ text.trim().length.toLocaleString() }} / {{ NOTE_TEXT_MAX.toLocaleString() }}
            </span>
            <div class="flex-1" />
            <span class="hidden text-xs text-muted-foreground md:inline">Esc to cancel · Enter to save</span>
            <Button
                type="button"
                variant="ghost"
                class="h-11 md:h-8"
                @click="emit('cancel')">
                Cancel
            </Button>
            <Button
                type="submit"
                class="h-11 md:h-8"
                :disabled="!canSave || saving">
                <LoaderCircle
                    v-if="saving"
                    class="animate-spin"
                    aria-hidden="true" />
                Save
            </Button>
        </div>
    </form>
</template>

<script setup lang="ts">
    import { useMediaQuery } from "@vueuse/core";
    import { LoaderCircle, ScrollText } from "lucide-vue-next";
    import type { SessionNote } from "~/utils/api/types";
    import { NOTE_TEXT_MAX, enterAction, postableText } from "~/utils/composer";

    const props = defineProps<{
        note: SessionNote;
        saving: boolean;
    }>();
    const emit = defineEmits<{
        save: [edit: { text: string; isRecap: boolean }];
        cancel: [];
    }>();

    const text = ref(props.note.text);
    const isRecap = ref(props.note.isRecap);
    const canSave = computed(() => postableText(text.value) !== null);
    const overLimit = computed(() => text.value.trim().length > NOTE_TEXT_MAX);

    const touch = useMediaQuery("(pointer: coarse)");
    const textarea = useTemplateRef<HTMLTextAreaElement>("textarea");

    function save() {
        const next = postableText(text.value);
        if (next === null || props.saving) return;
        emit("save", { text: next, isRecap: isRecap.value });
    }

    function onKeydown(event: KeyboardEvent) {
        if (event.key === "Escape") {
            event.preventDefault();
            emit("cancel");
            return;
        }
        if (enterAction(event, touch.value) === "post") {
            event.preventDefault();
            save();
        }
    }

    function grow() {
        const el = textarea.value;
        if (!el) return;
        el.style.height = "auto";
        el.style.height = `${el.scrollHeight + (el.offsetHeight - el.clientHeight)}px`;
    }

    onMounted(() => {
        grow();
        const el = textarea.value;
        el?.focus();
        el?.setSelectionRange(el.value.length, el.value.length);
    });
</script>
