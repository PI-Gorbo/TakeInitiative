<template>
    <!-- One note's ✨ suggestions (23e.2): a bottom sheet on a phone and a right-hand panel from
         `md`, like the evidence sheet. The note, then the same chips as on the loose-ends page
         (23d), then Edit to finish by hand. Nothing happens until the author taps.
         23f: "Look again" asks the model for a deeper pass over this one note. This component
         stays presentational — the chip decides what each tap does and what `status` says.
         SAM-13: "+ Create" opens its form here, under the chip, instead of closing this panel
         for a dialog. Linking, creating and dismissing all leave the author where they are, so
         they can work through a note's suggestions without the panel going anywhere. -->
    <DialogRoot v-model:open="open">
        <DialogPortal>
            <DialogOverlay
                class="fixed inset-0 z-50 bg-black/80 data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
            <DialogContent
                class="fixed inset-x-0 bottom-0 z-50 flex max-h-[85dvh] flex-col rounded-t-xl border-t bg-background pb-safe shadow-lg outline-none data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:slide-out-to-bottom data-[state=open]:slide-in-from-bottom md:inset-y-0 md:left-auto md:right-0 md:max-h-none md:w-full md:max-w-md md:rounded-none md:border-l md:border-t-0 md:pb-0 md:data-[state=closed]:slide-out-to-right md:data-[state=open]:slide-in-from-right md:data-[state=closed]:slide-out-to-bottom-0 md:data-[state=open]:slide-in-from-bottom-0">
                <header
                    class="flex shrink-0 items-center gap-2 border-b py-1 pl-4 pr-1">
                    <DialogTitle
                        class="min-w-0 flex-1 truncate text-base font-semibold">
                        ✨ Suggestions for your note
                    </DialogTitle>
                    <DialogDescription class="sr-only">
                        Links the suggestion model proposes in your note.
                        Nothing is linked or created until you tap one.
                    </DialogDescription>
                    <DialogClose
                        class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        aria-label="Close">
                        <X
                            class="size-5"
                            aria-hidden="true" />
                    </DialogClose>
                </header>

                <div
                    class="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto px-4 py-3">
                    <SessionNoteMarkdown
                        :campaignId="campaignId"
                        :text="note.text"
                        class="rounded-md border px-3 py-2" />
                    <p
                        v-if="status"
                        :role="status.isError ? 'alert' : 'status'"
                        :class="[
                            'text-sm',
                            status.isError
                                ? 'text-destructive-tint'
                                : 'text-muted-foreground',
                        ]">
                        {{ status.text }}
                    </p>
                    <p
                        v-if="!working && suggestions.length === 0"
                        role="status"
                        class="text-sm text-muted-foreground">
                        {{
                            canLookAgain
                                ? "Nothing here yet. Look again to have the model try harder."
                                : NOTHING_DEEPER_LABEL
                        }}
                    </p>
                    <LooseEndsModelSuggestions
                        v-else-if="suggestions.length > 0"
                        ref="chips"
                        :suggestions="suggestions"
                        :disabled="busy || !!createFor"
                        :unsureBelow="unsureBelow"
                        :expandedKey="expandedKey"
                        @link="(s) => emit('link', s)"
                        @create="(s) => emit('create', s)"
                        @dismiss="(s) => emit('dismiss', s)">
                        <!-- The chip the author tapped, opened out in place. -->
                        <template #create="{ suggestion }">
                            <div
                                class="flex flex-col gap-3 rounded-md border border-gold/40 p-3">
                                <p class="text-sm text-muted-foreground">
                                    “{{ suggestion.text }}” in your note becomes
                                    a mention of the new entry.
                                </p>
                                <SuggestionsCreateSuggestionForm
                                    :campaignId="campaignId"
                                    :note="note"
                                    :suggestion="suggestion"
                                    :model="model"
                                    autofocus
                                    @cancel="cancelCreate(suggestion)"
                                    @done="(r) => emit('created', r)" />
                            </div>
                        </template>
                    </LooseEndsModelSuggestions>
                    <p
                        v-if="!canLookAgain && suggestions.length > 0"
                        class="text-xs text-muted-foreground">
                        {{ NOTHING_DEEPER_LABEL }}
                    </p>
                    <p class="text-xs text-muted-foreground">
                        From the suggestion model on this device. Your note was
                        not sent anywhere to be read.
                    </p>
                </div>

                <footer
                    class="flex shrink-0 items-center justify-between gap-2 border-t px-4 py-2">
                    <Button
                        v-if="canLookAgain"
                        variant="ghost"
                        class="h-11 gap-1 text-gold md:h-9"
                        :disabled="busy || working || !!createFor"
                        :aria-label="`${LOOK_AGAIN_LABEL}: read this note again, looking harder`"
                        @click="emit('lookAgain')">
                        <span aria-hidden="true">✨</span>
                        {{ working ? "Looking…" : LOOK_AGAIN_LABEL }}
                    </Button>
                    <span v-else />
                    <Button
                        variant="ghost"
                        class="h-11 gap-1 md:h-9"
                        :disabled="busy || !!createFor"
                        aria-label="Edit this note to link it by hand"
                        @click="emit('edit')">
                        <Pencil
                            class="size-4"
                            aria-hidden="true" />
                        Edit
                    </Button>
                </footer>
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { Pencil, X } from "lucide-vue-next";
    import {
        DialogClose,
        DialogContent,
        DialogDescription,
        DialogOverlay,
        DialogPortal,
        DialogRoot,
        DialogTitle,
    } from "reka-ui";
    import type { SessionNote } from "~/utils/api/types";
    import {
        LOOK_AGAIN_LABEL,
        NOTHING_DEEPER_LABEL,
        modelSuggestionKey,
        type ModelSuggestion,
        type SuggestionModel,
    } from "~/utils/suggestions";

    const props = defineProps<{
        campaignId: string;
        note: SessionNote;
        suggestions: readonly ModelSuggestion[];
        /** The model an accepted suggestion records (23c's provenance). */
        model: SuggestionModel;
        /** While a link is being saved. */
        busy?: boolean;
        /** 23f: what the model is doing, or what went wrong. */
        status?: { text: string; isError?: boolean } | null;
        /** 23f: true while the model is loading or reading this note. */
        working?: boolean;
        /** 23f: false once the deepest pass has run — there is nothing more to ask for. */
        canLookAgain?: boolean;
        /** 23f: the pinned threshold, on a deeper pass: under it a chip is marked "unsure". */
        unsureBelow?: number;
        /** SAM-13: the suggestion whose create form is open under its chip, if any. */
        createFor?: ModelSuggestion | null;
    }>();
    const open = defineModel<boolean>("open", { required: true });
    const emit = defineEmits<{
        link: [suggestion: ModelSuggestion];
        create: [suggestion: ModelSuggestion];
        dismiss: [suggestion: ModelSuggestion];
        cancelCreate: [];
        created: [result: { name: string; created: boolean }];
        edit: [];
        lookAgain: [];
    }>();

    const chips = useTemplateRef<{
        focusChip: (s: ModelSuggestion) => void;
    }>("chips");
    const expandedKey = computed(() =>
        props.createFor ? modelSuggestionKey(props.createFor) : undefined
    );

    /** Cancel closes the form and hands the focus back to the chip it came from. */
    async function cancelCreate(suggestion: ModelSuggestion) {
        emit("cancelCreate");
        await nextTick();
        chips.value?.focusChip(suggestion);
    }
</script>
