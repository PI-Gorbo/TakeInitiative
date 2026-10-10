<template>
    <!-- "✨ … looks like a Place · + Create" on the loose-ends page (23d.5): a bottom sheet on a
         phone, a dialog from `md`, around `CreateSuggestionForm`'s fields. The stream's
         suggestions sheet does not use this — it opens the same form inline, so actioning a
         suggestion never takes the author anywhere (SAM-13). -->
    <DialogRoot v-model:open="open">
        <DialogPortal>
            <DialogOverlay
                class="fixed inset-0 z-50 bg-black/80 data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
            <DialogContent
                class="fixed inset-x-0 bottom-0 z-50 flex max-h-[90dvh] flex-col gap-4 overflow-y-auto rounded-t-xl border-t bg-background px-4 pt-4 pb-safe shadow-lg outline-none data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:slide-out-to-bottom data-[state=open]:slide-in-from-bottom md:inset-x-auto md:bottom-auto md:left-1/2 md:top-1/2 md:w-full md:max-w-md md:-translate-x-1/2 md:-translate-y-1/2 md:rounded-xl md:border md:pb-4 md:data-[state=closed]:slide-out-to-bottom-0 md:data-[state=open]:slide-in-from-bottom-0">
                <header class="flex items-start gap-2">
                    <div class="min-w-0 flex-1">
                        <DialogTitle class="text-base font-semibold"
                            >✨ Create an entry and link it</DialogTitle
                        >
                        <DialogDescription
                            class="text-sm text-muted-foreground">
                            Suggested by the suggestion model. “{{
                                suggestion?.text
                            }}” in your note becomes a mention of the new entry.
                        </DialogDescription>
                    </div>
                    <DialogClose
                        class="-mr-2 -mt-2 flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        aria-label="Close">
                        <X
                            class="size-5"
                            aria-hidden="true" />
                    </DialogClose>
                </header>

                <SuggestionsCreateSuggestionForm
                    :campaignId="campaignId"
                    :note="note"
                    :suggestion="suggestion"
                    :model="model"
                    @cancel="open = false"
                    @done="finish" />
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { X } from "lucide-vue-next";
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
    import type { ModelSuggestion, SuggestionModel } from "~/utils/suggestions";

    defineProps<{
        campaignId: string;
        note: SessionNote;
        suggestion: ModelSuggestion | null;
        model: SuggestionModel;
    }>();
    const open = defineModel<boolean>("open", { required: true });
    const emit = defineEmits<{ done: [] }>();

    function finish() {
        open.value = false;
        emit("done");
    }
</script>
