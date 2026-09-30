<template>
    <!-- One note's ✨ suggestions (23e.2): a bottom sheet on a phone and a right-hand panel from
         `md`, like the evidence sheet. The note, then the same chips as on the loose-ends page
         (23d), then Edit to finish by hand. Nothing happens until the author taps. -->
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
                        v-if="suggestions.length === 0"
                        role="status"
                        class="text-sm text-muted-foreground">
                        No more suggestions for this note.
                    </p>
                    <LooseEndsModelSuggestions
                        v-else
                        :suggestions="suggestions"
                        :disabled="busy"
                        @link="(s) => emit('link', s)"
                        @create="(s) => emit('create', s)"
                        @dismiss="(s) => emit('dismiss', s)" />
                    <p class="text-xs text-muted-foreground">
                        From the suggestion model on this device. Your note was
                        not sent anywhere to be read.
                    </p>
                </div>

                <footer
                    class="flex shrink-0 justify-end gap-2 border-t px-4 py-2">
                    <Button
                        variant="ghost"
                        class="h-11 gap-1 md:h-9"
                        :disabled="busy"
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
    import type { ModelSuggestion } from "~/utils/suggestions";

    defineProps<{
        campaignId: string;
        note: Pick<SessionNote, "id" | "text">;
        suggestions: readonly ModelSuggestion[];
        /** While a link is being saved. */
        busy?: boolean;
    }>();
    const open = defineModel<boolean>("open", { required: true });
    const emit = defineEmits<{
        link: [suggestion: ModelSuggestion];
        create: [suggestion: ModelSuggestion];
        dismiss: [suggestion: ModelSuggestion];
        edit: [];
    }>();
</script>
