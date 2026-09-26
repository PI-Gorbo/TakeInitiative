<template>
    <!-- Editing a note in place (author only). Enter saves on desktop, Esc cancels. The
         text reads mentions as `@[Name]`, like the composer (15d), with the same `@`
         picker: a popover below the caret from md, and on a phone the mention strip
         docked above the keyboard. -->
    <form
        class="flex flex-col gap-1 py-1"
        aria-label="Edit note"
        @submit.prevent="save">
        <div class="relative flex flex-col gap-1">
            <textarea
                ref="textarea"
                v-model="state.text"
                aria-label="Note text"
                :enterkeyhint="touch ? 'enter' : 'done'"
                :aria-controls="mentions.open.value ? `${id}-mentions` : undefined"
                :aria-activedescendant="mentions.open.value ? `${id}-mentions-${mentions.highlighted.value}` : undefined"
                class="max-h-[50dvh] min-h-11 w-full resize-none overflow-y-auto rounded-md border bg-background px-3 py-2 text-base leading-snug outline-none focus-visible:ring-1 focus-visible:ring-ring md:text-sm"
                @keydown="onKeydown"
                @input="grow" />
            <ComposerMentionStrip
                :picker="mentions"
                :listId="`${id}-mentions`"
                placement="below"
                docked />
            <ComposerMentionLinks
                :state="state"
                :directory="mentions.directory.value" />
        </div>
        <div class="flex flex-wrap items-center gap-1">
            <button
                type="button"
                title="Mention an entry"
                aria-label="Mention an entry"
                class="flex h-11 min-w-11 items-center justify-center rounded-md px-2 text-muted-foreground hover:bg-accent hover:text-accent-foreground md:h-8 md:min-w-8"
                @mousedown.prevent
                @click="mentions.trigger()">
                <AtSign
                    class="size-4"
                    aria-hidden="true" />
            </button>
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
                {{ storedLength.toLocaleString() }} / {{ NOTE_TEXT_MAX.toLocaleString() }}
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
        <ComposerRevealDialog
            ref="reveal"
            :campaignId="campaignId" />
    </form>
</template>

<script setup lang="ts">
    import { useQueryClient } from "@tanstack/vue-query";
    import { useMediaQuery } from "@vueuse/core";
    import { AtSign, LoaderCircle, ScrollText } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import type { SessionNote, Visibility } from "~/utils/api/types";
    import { NOTE_TEXT_MAX, enterAction, postableText } from "~/utils/composer";
    import type { EntryViewer } from "~/utils/entries";
    import {
        fromStoredText,
        mentionBody,
        newEntryErrorFrom,
        relinkNewEntry,
        revealCheck,
        toStoredText,
        type MentionText,
        type RevealItem,
    } from "~/utils/mentions";
    import { invalidateEntries } from "~/utils/queries/entries";
    import { invalidateSessionStreams, putNoteMutation } from "~/utils/queries/sessions";

    const props = defineProps<{
        campaignId: string;
        note: SessionNote;
        viewer: EntryViewer;
    }>();
    const emit = defineEmits<{ saved: []; cancel: [] }>();

    const id = useId();
    // The stored form back to `@[Name]` plus links (15d).
    const state = reactive<MentionText>({ ...fromStoredText(props.note.text), newEntries: [] });
    const isRecap = ref(props.note.isRecap);
    const storedLength = computed(() => toStoredText(state.text, state.links).trim().length);
    const canSave = computed(() => postableText(state.text) !== null && storedLength.value <= NOTE_TEXT_MAX);
    const overLimit = computed(() => storedLength.value > NOTE_TEXT_MAX);

    const touch = useMediaQuery("(pointer: coarse)");
    const textarea = useTemplateRef<HTMLTextAreaElement>("textarea");
    const mentions = useMentionPicker({ state, textarea, campaignId: () => props.campaignId });
    const reveal = useTemplateRef<{ confirm: (v: Visibility, items: RevealItem[]) => Promise<boolean> }>("reveal");

    const putNote = putNoteMutation();
    const queryClient = useQueryClient();
    const saving = computed(() => putNote.isPending.value);

    async function save() {
        const trimmed = postableText(state.text);
        if (trimmed === null || saving.value) return;
        const body = mentionBody(state, trimmed);
        if (body.text === props.note.text && isRecap.value === props.note.isRecap && body.newEntries.length === 0) {
            emit("saved");
            return;
        }
        // A hidden `Everyone` note is read by the DMs and its author only.
        const audience: Visibility =
            props.note.isHidden && props.note.visibility === "Everyone" ? "DM" : props.note.visibility;
        const warn = revealCheck(audience, body.text, mentions.directory.value, props.viewer);
        if (warn.length > 0 && !(await reveal.value?.confirm(audience, warn))) return;

        try {
            await putNote.mutateAsync({
                campaignId: props.campaignId,
                noteId: props.note.id,
                text: body.text,
                isRecap: isRecap.value,
                ...(body.newEntries.length > 0 ? { newEntries: body.newEntries } : {}),
            });
            emit("saved");
        } catch (error) {
            const entryError = newEntryErrorFrom(error);
            if (entryError?.kind === "alreadyCreated") {
                // A retry after a timeout: the first save went through.
                toast.info("That edit was already saved.");
                void invalidateSessionStreams(queryClient, props.campaignId);
                void invalidateEntries(queryClient, props.campaignId);
                emit("saved");
                return;
            }
            if (entryError?.kind === "duplicate") {
                const name = state.newEntries.find(
                    (e) => e.id.toLowerCase() === entryError.newEntryId.toLowerCase()
                )?.name;
                Object.assign(state, relinkNewEntry(state, entryError.newEntryId, entryError.existingEntryId));
                toast.error(`"${name ?? "That entry"}" already exists, so the mention now links to it. Save again.`);
                return;
            }
            toast.error(apiErrorMessage(error, "Could not save the note."));
        }
    }

    function onKeydown(event: KeyboardEvent) {
        if (mentions.onKeydown(event)) return;
        if (event.key === "Escape") {
            event.preventDefault();
            emit("cancel");
            return;
        }
        if (enterAction(event, touch.value) === "post") {
            event.preventDefault();
            void save();
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
