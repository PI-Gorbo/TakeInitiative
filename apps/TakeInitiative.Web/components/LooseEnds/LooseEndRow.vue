<template>
    <!-- One loose end (design §5), resolved where it is listed: a note is linked by a
         suggestion or edited in the composer, an `Other` entry gets a kind, an empty
         article is written. A resolved row shows ✓ until the list lets it go. -->
    <article
        :id="rowId"
        :data-loose-note="note?.id"
        :aria-labelledby="`${rowId}-kind`"
        :class="[
            'flex flex-col gap-2 rounded-lg border px-3 py-2.5 transition-colors duration-700',
            highlighted && 'bg-gold/15',
            resolved && 'opacity-70',
        ]">
        <header class="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-muted-foreground">
            <span
                :id="`${rowId}-kind`"
                class="font-semibold uppercase tracking-wide">
                {{ looseEndRowLabel(item.kind, suggestions?.count ?? 0) }}
            </span>
            <NuxtLink
                v-if="note && item.sessionNumber"
                :to="noteHref"
                class="-my-2 flex min-h-11 items-center rounded font-medium text-gold hover:underline md:min-h-0"
                :aria-label="`Session ${item.sessionNumber}, ${formatNoteTime(note.postedAt)}: open this note in the session stream`">
                S{{ item.sessionNumber }} · {{ formatNoteTime(note.postedAt) }}
            </NuxtLink>
            <span v-else-if="item.sessionNumber">from S{{ item.sessionNumber }}</span>
            <span
                v-if="note && note.visibility !== 'Everyone'"
                class="rounded border px-1.5 font-medium">
                🔒 {{ note.visibility }}
            </span>
            <span
                v-if="note?.isHidden"
                class="text-destructive-tint">
                Hidden by a DM
            </span>
            <span
                v-if="resolved"
                role="status"
                class="ml-auto flex items-center gap-1 font-medium text-gold">
                <Check
                    class="size-4"
                    aria-hidden="true" />
                Resolved
            </span>
        </header>

        <!-- An unlinked note or an untagged image: the note, then its suggestions. -->
        <template v-if="note">
            <ImageNoteImages
                v-if="note.images.length > 0"
                :campaignId="campaignId"
                :images="note.images"
                :text="note.text"
                :authorName="authorName"
                compact
                @open="(imageId) => emit('openImage', imageId)" />
            <SessionNoteMarkdown
                v-if="note.text"
                :campaignId="campaignId"
                :text="note.text" />
            <p
                v-else
                class="text-sm italic text-muted-foreground">
                No caption.
            </p>
            <div
                v-if="!resolved"
                class="flex flex-wrap items-center gap-1.5">
                <LooseEndsLinkSuggestions
                    :suggestions="suggestions?.links ?? item.suggestions"
                    :disabled="busy"
                    @link="link" />
                <LooseEndsModelSuggestions
                    v-if="suggestions"
                    :suggestions="suggestions.model"
                    :disabled="busy"
                    @link="acceptMatch"
                    @create="startCreate"
                    @dismiss="(s) => modelSuggestions?.dismiss(s)" />
                <Button
                    variant="ghost"
                    class="h-11 gap-1 px-2 text-muted-foreground md:h-8"
                    :disabled="busy"
                    :aria-label="note.images.length > 0 ? 'Edit this note to tag it or add a caption' : 'Edit this note to link it'"
                    @click="edit">
                    <Pencil
                        class="size-4"
                        aria-hidden="true" />
                    Edit
                </Button>
            </div>
            <SuggestionsCreateFromSuggestion
                v-if="modelSuggestions"
                v-model:open="creating"
                :campaignId="campaignId"
                :note="note"
                :suggestion="createFrom"
                :model="modelSuggestions.model"
                @done="emit('resolved')" />
        </template>

        <!-- An entry of kind Other: the kind chips, one tap sets it. -->
        <template v-else-if="entry && item.kind === 'OtherKind'">
            <p class="text-sm">
                <NuxtLink
                    :to="entryHref(campaignId, entry.id)"
                    class="font-medium text-gold hover:underline">
                    {{ ENTRY_KIND_ICONS[entry.kind] }} {{ entry.name }}
                </NuxtLink>
                <span class="text-muted-foreground"> · {{ mentionCountLabel(item.mentionCount ?? 0) }}</span>
            </p>
            <WikiChoiceChips
                v-if="!resolved"
                :label="`Kind of ${entry.name}`"
                :options="ENTRY_KINDS"
                :modelValue="entry.kind"
                @update:modelValue="setKind" />
        </template>

        <!-- An empty article: write it, or promote a note from the timeline. -->
        <template v-else-if="entry">
            <p class="text-sm">
                <NuxtLink
                    :to="entryHref(campaignId, entry.id)"
                    class="font-medium text-gold hover:underline">
                    {{ ENTRY_KIND_ICONS[entry.kind] }} {{ entry.name }}
                </NuxtLink>
                <span class="text-muted-foreground">
                    has {{ mentionCountLabel(item.mentionCount ?? 0) }} and no summary
                </span>
            </p>
            <div class="flex flex-wrap gap-1.5">
                <Button
                    as-child
                    class="h-11 gap-1 md:h-8">
                    <NuxtLink :to="entryHref(campaignId, entry.id, WRITE_ARTICLE)">
                        <PenLine
                            class="size-4"
                            aria-hidden="true" />
                        Write
                    </NuxtLink>
                </Button>
                <Button
                    as-child
                    variant="outline"
                    class="h-11 md:h-8">
                    <NuxtLink :to="{ path: entryHref(campaignId, entry.id), hash: `#${ENTRY_TIMELINE_ANCHOR}` }">
                        Pick from notes
                    </NuxtLink>
                </Button>
            </div>
        </template>
    </article>
</template>

<script setup lang="ts">
    import { Check, PenLine, Pencil } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { EntryKind, LinkSuggestion, LooseEnd } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import { ENTRY_TIMELINE_ANCHOR, WRITE_ARTICLE, entryHref } from "~/utils/article";
    import { ENTRY_KINDS, ENTRY_KIND_ICONS } from "~/utils/entries";
    import { LOOSE_END_SUGGESTIONS } from "~/composables/useLooseEndSuggestions";
    import { linkSpan, looseEndKey, looseEndRowLabel, mentionCountLabel } from "~/utils/looseEnds";
    import { NOTE_LINK_PARAM } from "~/utils/noteActions";
    import { putEntryKindMutation } from "~/utils/queries/entries";
    import { formatNoteTime } from "~/utils/sessionDates";
    import type { LinkChip, ModelSuggestion } from "~/utils/suggestions";

    const props = defineProps<{
        campaignId: string;
        item: LooseEnd;
        /** The note's author, for image alt text: always the viewer (a loose end is theirs). */
        authorName: string;
        /** Resolved: shows ✓ until the list drops it. */
        resolved?: boolean;
        /** Briefly true after following a link to this note's loose end (the 16c hint). */
        highlighted?: boolean;
    }>();
    const emit = defineEmits<{
        resolved: [];
        openImage: [imageId: string];
    }>();

    const note = computed(() => props.item.note ?? null);
    const entry = computed(() => props.item.entry ?? null);
    const rowId = computed(() => `loose-${looseEndKey(props.item).replace(/[^\w-]/g, "-")}`);
    const noteHref = computed(() => ({
        path: `/app/campaigns/${encodeURIComponent(props.campaignId)}`,
        query: { [NOTE_LINK_PARAM]: note.value?.id ?? "" },
    }));

    // ✨ model suggestions (23d), from the page; none elsewhere or with the setting Off.
    const modelSuggestions = inject(LOOSE_END_SUGGESTIONS, null);
    const suggestions = computed(() => modelSuggestions?.forItem(props.item) ?? null);
    const creating = ref(false);
    const createFrom = ref<ModelSuggestion | null>(null);

    const { accept: acceptSuggestion, putNote } = useAcceptSuggestion(() => props.campaignId);
    const putKind = putEntryKindMutation();
    const busy = computed(() => putNote.isPending.value || putKind.isPending.value);

    /**
     * One tap: the span becomes a mention, the rest of the note is sent unchanged. A chip the
     * model also proposed for the same span records the model (23c), as its ✨ says.
     */
    async function link(suggestion: LinkSuggestion & Partial<Pick<LinkChip, "model">>) {
        const current = note.value;
        if (!current || busy.value) return;
        const model = suggestion.model;
        if (model && modelSuggestions && model.start === suggestion.start && model.length === suggestion.length) {
            return accept({ start: suggestion.start, text: suggestion.text, confidence: model.confidence }, suggestion.entry.id);
        }
        const text = linkSpan(current.text, suggestion, suggestion.entry.id);
        if (text === null) {
            toast.error(`“${suggestion.text}” is no longer in the note. Edit it to link it.`);
            return;
        }
        try {
            await putNote.mutateAsync({
                campaignId: props.campaignId,
                noteId: current.id,
                text,
                isRecap: current.isRecap,
            });
            emit("resolved");
        } catch (error) {
            toast.error(apiErrorMessage(error, "Could not link the note."));
        }
    }

    /** "✨ Rellan → @Rellan Ashvale?": one tap links it, with the model on the edit (23c). */
    function acceptMatch(s: ModelSuggestion) {
        if (s.match) void accept(s, s.match.entry.id);
    }

    async function accept(s: Pick<ModelSuggestion, "start" | "text" | "confidence">, entryId: string) {
        const current = note.value;
        if (!current || busy.value || !modelSuggestions) return;
        if (await acceptSuggestion(current, s, entryId, modelSuggestions.model)) emit("resolved");
    }

    /** "✨ Greyhollow Keep looks like a Place · + Create": nothing is created until the dialog's tap. */
    function startCreate(s: ModelSuggestion) {
        createFrom.value = s;
        creating.value = true;
    }

    async function setKind(kind: EntryKind) {
        const current = entry.value;
        if (!current || busy.value || kind === current.kind) return;
        try {
            await putKind.mutateAsync({ campaignId: props.campaignId, entryId: current.id, kind });
            emit("resolved");
        } catch (error) {
            toast.error(apiErrorMessage(error, "Could not set the kind."));
        }
    }

    /** Edit happens in the composer (step 17), on the Campaign tab, at the note. */
    function edit() {
        const current = note.value;
        if (!current) return;
        useComposerEdit(props.campaignId).start(current, props.item.sessionNumber ?? null);
        void navigateTo(noteHref.value);
    }
</script>
