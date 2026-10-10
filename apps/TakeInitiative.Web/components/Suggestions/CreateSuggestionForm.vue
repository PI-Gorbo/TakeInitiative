<template>
    <!-- The fields behind "+ Create" (23d.5): the name (the span, editable) and the kind (the
         model's, picked) are the author's to change; the entry gets the note's visibility, as a
         composer create does. Nothing is created until "Create and link". If the name is already
         an entry, it offers that entry instead.
         No chrome of its own, so it fits both `CreateFromSuggestion`'s dialog on the loose-ends
         page and the suggestions sheet's inline panel (SAM-13). -->
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
                :aria-describedby="error ? `${id}-error` : undefined" />
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
                Link to {{ existing.name ? `@${existing.name}` : "it" }}
                instead
            </Button>
        </div>

        <footer class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            <Button
                type="button"
                variant="ghost"
                class="h-11 md:h-9"
                @click="emit('cancel')">
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
</template>

<script setup lang="ts">
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
        modelSuggestionKey,
        type ModelSuggestion,
        type SuggestionModel,
    } from "~/utils/suggestions";

    const props = defineProps<{
        campaignId: string;
        note: SessionNote;
        suggestion: ModelSuggestion | null;
        model: SuggestionModel;
        /** Takes the focus on mount, for the panel that opens without a dialog to do it. */
        autofocus?: boolean;
    }>();
    const emit = defineEmits<{
        cancel: [];
        /** The entry the note now mentions, for the caller to say so. */
        done: [result: { name: string; created: boolean }];
    }>();

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

    // The span, not the note: a stream refresh replaces `note` and must not reset what the
    // author has typed. Mounted per "+ Create", so this is really just the starting state.
    watch(
        () => (props.suggestion ? modelSuggestionKey(props.suggestion) : null),
        () => {
            const d = defaults.value;
            if (!d) return;
            name.value = d.name;
            kind.value = d.kind;
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

    // The inline panel has no dialog to move the focus for it.
    onMounted(() => {
        if (props.autofocus) document.getElementById(`${id}-name`)?.focus();
    });

    async function put(entryId: string, create: boolean) {
        const s = props.suggestion;
        if (!s) return;
        const typed = name.value.trim();
        const body = acceptBody(
            props.note,
            s,
            entryId,
            props.model,
            create ? { name: typed, kind: kind.value } : undefined
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
            emit("done", {
                name: create ? typed : (existing.value?.name ?? typed),
                created: create,
            });
        } catch (err) {
            const clash =
                apiErrorStatus(err) === 409 ? existingEntryIdFrom(err) : null;
            if (clash) {
                existing.value = { id: clash, name: null };
                checkedName = typed;
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
