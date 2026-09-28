<template>
    <!-- "✨ … looks like a Place · + Create" (23d.5): a bottom sheet on a phone, a dialog from
         `md`. The name (the span, editable) and the kind (the model's, picked) are the author's
         to change; the entry gets the note's visibility, as a composer create does. Nothing is
         created until "Create and link". If the name is already an entry, it offers that
         entry instead. -->
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

                <form
                    class="flex flex-col gap-4"
                    @submit.prevent="submit">
                    <div class="flex flex-col gap-1.5">
                        <Label :for="`${id}-name`">Name</Label>
                        <Input
                            :id="`${id}-name`"
                            v-model="name"
                            maxlength="100"
                            autocomplete="off"
                            enterkeyhint="done"
                            class="h-11 text-base md:h-9 md:text-sm"
                            :aria-invalid="!!error || undefined"
                            :aria-describedby="
                                error ? `${id}-error` : undefined
                            " />
                        <p
                            v-if="error"
                            :id="`${id}-error`"
                            class="text-sm text-destructive-tint"
                            role="alert">
                            {{ error }}
                        </p>
                    </div>
                    <div class="flex flex-col gap-1.5">
                        <span class="text-sm font-medium">Kind</span>
                        <ComposerKindChips
                            :modelValue="kind"
                            @pick="(k) => (kind = k)" />
                    </div>
                    <p class="text-sm">
                        <span class="text-muted-foreground">Visible to</span>
                        {{ visibilityLabel }}
                        <span class="text-muted-foreground">(the note's)</span>
                    </p>

                    <div
                        v-if="existing"
                        class="flex flex-col gap-2 rounded-md border border-gold/40 p-2 text-sm"
                        role="status">
                        <p>
                            <span class="text-gold">{{
                                existing.name ? `@${existing.name}` : "An entry"
                            }}</span>
                            is already in the wiki.
                        </p>
                        <Button
                            type="button"
                            variant="outline"
                            class="h-11 w-fit md:h-9"
                            :disabled="busy"
                            @click="linkInstead(existing.id)">
                            Link to
                            {{
                                existing.name ? `@${existing.name}` : "it"
                            }}
                            instead
                        </Button>
                    </div>

                    <footer
                        class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                        <Button
                            type="button"
                            variant="ghost"
                            class="h-11 md:h-9"
                            @click="open = false">
                            Cancel
                        </Button>
                        <Button
                            type="submit"
                            class="h-11 md:h-9"
                            :disabled="!name.trim() || busy">
                            {{ existing ? "Create anyway" : "Create and link" }}
                        </Button>
                    </footer>
                </form>
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
    import type { EntryKind, SessionNote } from "~/utils/api/types";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import {
        ENTRY_VISIBILITY_OPTIONS,
        existingEntryIdFrom,
    } from "~/utils/entries";
    import { putNoteMutation } from "~/utils/queries/sessions";
    import { matchSuggestionSpans } from "~/utils/queries/suggestions";
    import {
        acceptBody,
        createDefaults,
        isLinkableSpan,
        type ModelSuggestion,
        type SuggestionModel,
    } from "~/utils/suggestions";

    const props = defineProps<{
        campaignId: string;
        note: SessionNote;
        suggestion: ModelSuggestion | null;
        model: SuggestionModel;
    }>();
    const open = defineModel<boolean>("open", { required: true });
    const emit = defineEmits<{ done: [] }>();

    const id = useId();
    const api = useApi();
    const putNote = putNoteMutation();
    const name = ref("");
    const kind = ref<EntryKind>("Character");
    const error = ref<string | null>(null);
    /** An entry the name already is (checked on submit), offered instead. */
    const existing = ref<{ id: string; name: string | null } | null>(null);
    /** The name `existing` was checked for: "Create anyway" skips the check for it. */
    let checkedName: string | null = null;
    const checking = ref(false);
    const busy = computed(() => checking.value || putNote.isPending.value);

    const defaults = computed(() =>
        props.suggestion ? createDefaults(props.suggestion, props.note) : null
    );
    const visibilityLabel = computed(
        () =>
            ENTRY_VISIBILITY_OPTIONS.find(
                (o) => o.value === defaults.value?.visibility
            )?.label ?? defaults.value?.visibility
    );

    watch(
        open,
        (isOpen) => {
            if (!isOpen || !defaults.value) return;
            name.value = defaults.value.name;
            kind.value = defaults.value.kind;
            error.value = null;
            existing.value = null;
            checkedName = null;
        },
        { immediate: true }
    );
    watch(name, () => {
        error.value = null;
        if (existing.value && name.value.trim() !== checkedName)
            existing.value = null;
    });

    async function put(entryId: string, create: boolean) {
        const s = props.suggestion;
        if (!s) return;
        const body = acceptBody(
            props.note,
            s,
            entryId,
            props.model,
            create ? { name: name.value, kind: kind.value } : undefined
        );
        if (!body) {
            error.value = `“${s.text}” is no longer in the note. Edit it to link it.`;
            return;
        }
        try {
            await putNote.mutateAsync({
                campaignId: props.campaignId,
                noteId: props.note.id,
                ...body,
            });
            open.value = false;
            emit("done");
        } catch (err) {
            const clash =
                apiErrorStatus(err) === 409 ? existingEntryIdFrom(err) : null;
            if (clash) {
                existing.value = { id: clash, name: null };
                checkedName = name.value.trim();
            }
            error.value = apiErrorMessage(
                err,
                create
                    ? "Could not create the entry."
                    : "Could not link the note."
            );
        }
    }

    /** "Create and link": first checks the name against the wiki (the author may have typed an existing one). */
    async function submit() {
        const typed = name.value.trim();
        if (!typed || busy.value) return;
        if (typed !== checkedName && isLinkableSpan(typed)) {
            checking.value = true;
            try {
                const [match] = await matchSuggestionSpans(
                    api,
                    props.campaignId,
                    [{ text: typed, kind: kind.value }]
                );
                checkedName = typed;
                if (match) {
                    existing.value = {
                        id: match.entry.id,
                        name: match.entry.name,
                    };
                    return;
                }
            } catch {
                // The check is a courtesy: the API still refuses a duplicate name with a 409.
            } finally {
                checking.value = false;
            }
        }
        await put(crypto.randomUUID(), true);
    }

    function linkInstead(entryId: string) {
        void put(entryId, false);
    }
</script>
