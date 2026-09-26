<template>
    <!-- Promote (15f, design §4 and §3a): a session note, or a selection of it, into an
         entry's article as a quote that links back to the note. The text box holds
         the excerpt, which can only be trimmed usefully: the server refuses anything
         that is not text from the note. On a phone the whole note goes in, and the
         article editor opens at the new quote to trim it there. -->
    <Dialog v-model:open="open">
        <!-- On a phone it sits at the top and stops above the keyboard (invariant 11). -->
        <DialogContent
            class="max-h-[90dvh] max-w-md overflow-y-auto max-md:top-4 max-md:translate-y-0"
            :style="phone ? { maxHeight: `calc(100dvh - ${inset}px - 2rem)` } : undefined">
            <DialogHeader>
                <DialogTitle>Promote to wiki</DialogTitle>
                <DialogDescription>
                    Adds
                    {{ excerpt === undefined ? "this note" : "the selection" }}
                    to an entry's article as a quote that links back to the note.
                </DialogDescription>
            </DialogHeader>

            <form
                class="flex flex-col gap-3"
                @submit.prevent="promote">
                <WikiEntryPicker
                    v-model="target"
                    v-model:kind="kind"
                    :campaignId="campaignId"
                    :viewer="viewer"
                    :locked="!!entryId" />

                <label
                    v-if="!phone"
                    class="flex flex-col gap-1 text-sm">
                    <span class="font-medium">Quote</span>
                    <textarea
                        v-model="state.text"
                        rows="4"
                        class="max-h-[40dvh] w-full resize-y rounded-md border bg-background px-3 py-2 text-base leading-snug outline-none focus-visible:ring-1 focus-visible:ring-ring md:text-sm" />
                    <span
                        v-if="!isExcerpt"
                        class="text-xs text-destructive-tint"
                        >A quote must be text from the note. Trim it, don't rewrite it.</span
                    >
                </label>
                <p
                    v-else
                    class="line-clamp-4 whitespace-pre-wrap rounded-md border-l-2 border-gold/60 pl-3 text-sm text-muted-foreground">
                    {{ state.text }}
                </p>
                <p
                    v-if="phone"
                    class="text-xs text-muted-foreground">
                    The whole note goes in. Trim it in the article editor next.
                </p>

                <p
                    v-if="audience !== 'Everyone'"
                    class="rounded-md bg-muted px-3 py-2 text-sm">
                    🔒 Players won't see this quote{{ audience === "Me" ? ": only the note's author will." : "." }}
                </p>
                <p
                    v-if="alreadyQuoted"
                    class="rounded-md bg-gold/10 px-3 py-2 text-sm">
                    {{ targetName }}'s article already quotes this note. Promoting again adds another quote.
                </p>

                <DialogFooter class="gap-2">
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
                        :disabled="!canPromote || busy">
                        <LoaderCircle
                            v-if="busy"
                            class="animate-spin"
                            aria-hidden="true" />
                        Promote
                    </Button>
                </DialogFooter>
            </form>
        </DialogContent>
    </Dialog>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { useMediaQuery } from "@vueuse/core";
    import { LoaderCircle } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import type { EntryKind, EntryQuote, SessionNote } from "~/utils/api/types";
    import { entryHref, quotesNote } from "~/utils/article";
    import { existingEntryIdFrom, resolveEntry, type EntryViewer } from "~/utils/entries";
    import { fromStoredText, toStoredText } from "~/utils/mentions";
    import { collapseWhitespace, isExcerptOf, noteAudience, type PromoteTarget } from "~/utils/promote";
    import {
        createEntryMutation,
        getEntryQuery,
        promoteNoteMutation,
        useEntryDirectory,
    } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        note: SessionNote;
        viewer: EntryViewer;
        /** A selection, as the note's stored text (`selectionToSource`). Undefined: the whole note. */
        excerpt?: string;
        /** The entry, already picked (the timeline's Promote). */
        entryId?: string;
    }>();
    const open = defineModel<boolean>("open", { required: true });
    const emit = defineEmits<{ promoted: [result: EntryQuote] }>();

    const phone = useMediaQuery("(max-width: 767.98px)");
    const inset = useKeyboardInset();
    const directory = useEntryDirectory(() => props.campaignId);

    const target = ref<PromoteTarget | null>(null);
    const kind = ref<EntryKind>("Character");
    // The excerpt reads mentions as `@[Name]`, like every text box (15d).
    const state = reactive(fromStoredText(props.excerpt ?? props.note.text));

    // Each opening starts from the props: the selection or note may have changed.
    watch(
        open,
        (value) => {
            if (!value) return;
            Object.assign(state, fromStoredText(props.excerpt ?? props.note.text));
            const entry = props.entryId ? resolveEntry(directory.value, props.entryId) : undefined;
            target.value = entry ? { kind: "entry", entry } : null;
        },
        { immediate: true }
    );

    const audience = computed(() => noteAudience(props.note));
    const stored = computed(() => toStoredText(state.text, state.links).trim());
    const isExcerpt = computed(() => isExcerptOf(props.note.text, stored.value));
    const canPromote = computed(() => !!target.value && (phone.value || isExcerpt.value));

    // "Promoting the same text from the same note twice is allowed, and the web warns."
    const targetEntryId = computed(() => (target.value?.kind === "entry" ? target.value.entry.id : ""));
    const targetQuery = useQuery(getEntryQuery(() => props.campaignId, targetEntryId));
    const alreadyQuoted = computed(
        () => !!targetEntryId.value && quotesNote(targetQuery.data.value?.article.blocks, props.note.id)
    );
    const targetName = computed(() =>
        target.value?.kind === "entry" ? target.value.entry.name : (target.value?.name ?? "")
    );

    const create = createEntryMutation();
    const promoteNote = promoteNoteMutation();
    const busy = computed(() => create.isPending.value || promoteNote.isPending.value);

    async function promote() {
        const picked = target.value;
        if (!picked || !canPromote.value || busy.value) return;
        let entryId: string;
        let entryName: string;
        if (picked.kind === "create") {
            try {
                const entry = await create.mutateAsync({
                    campaignId: props.campaignId,
                    name: picked.name,
                    kind: kind.value,
                    // A new entry inherits the note's visibility (§4), as with `@` Create.
                    visibility: audience.value,
                });
                entryId = entry.id;
                entryName = entry.name;
                target.value = { kind: "entry", entry };
            } catch (error) {
                const existing = apiErrorStatus(error) === 409 ? existingEntryIdFrom(error) : null;
                const entry = existing ? resolveEntry(directory.value, existing) : undefined;
                if (entry) target.value = { kind: "entry", entry };
                toast.error(apiErrorMessage(error, "Could not create the entry."));
                return;
            }
        } else {
            entryId = picked.entry.id;
            entryName = picked.entry.name;
        }

        // The whole note goes without `text` (the phone's flow, and an untouched note).
        const whole = phone.value || collapseWhitespace(stored.value) === collapseWhitespace(props.note.text);
        try {
            const result = await promoteNote.mutateAsync({
                campaignId: props.campaignId,
                entryId,
                noteId: props.note.id,
                ...(whole ? {} : { text: stored.value }),
            });
            open.value = false;
            emit("promoted", result);
            if (phone.value) {
                // §3a: trim the quote in the article editor.
                await navigateTo(entryHref(props.campaignId, entryId, result.blockId));
                return;
            }
            toast.success(`Added to ${entryName}'s article.`, {
                action: {
                    label: "Open",
                    onClick: () => void navigateTo(entryHref(props.campaignId, entryId)),
                },
            });
        } catch (error) {
            const status = apiErrorStatus(error);
            toast.error(
                status === 404
                    ? "That note or entry is gone, or you cannot see it."
                    : status === 403
                      ? "You cannot edit that entry."
                      : apiErrorMessage(error, "Could not promote the note.")
            );
        }
    }
</script>
