<template>
    <!-- The knowledge-base prompt (28c): "✨ Is this the Beholder from the Monster Manual?" over the
         row's own detail line, with [ Link it ] and [ No ]. It sits directly above the Links
         section, because that is where its outcome lands.

         **It asks; it never guesses.** Nothing is linked without a tap, and no model is involved:
         the candidates are one SQL query over the entry's name and its aliases (28b), so there is no
         download, no worker and no confidence score. The row's detail line — "Monster · CR 13 · MM ·
         p. 28" — is the whole of what the prompt claims, and it is what lets the member judge.

         **Link it** is 27c's `POST links` and nothing else: an ordinary knowledge-base link, as this
         member. Its own invalidation takes the prompt away and draws the link line in its place, so
         there is no accept endpoint and no provenance to revert.

         **No** does not confirm. It is cheap and reversible — the knowledge-base picker still has the
         row — and it is campaign-wide, because "The Eye is not the Beholder" is a fact about the
         entry rather than one DM's preference. It is optimistic, and rolls back if the request fails.

         Both buttons are 44px: this is a decision made at the table on a phone. -->
    <section
        v-if="best"
        :aria-labelledby="`${id}-question`"
        class="flex flex-col gap-2 rounded-md border border-gold/40 bg-gold/5 p-3"
        :class="panel ? 'mx-4 my-4' : ''">
        <div class="flex min-w-0 flex-col gap-0.5">
            <p
                :id="`${id}-question`"
                class="text-sm font-medium">
                <span aria-hidden="true">✨ </span>{{ suggestionQuestion(best) }}
            </p>
            <p
                class="text-xs text-muted-foreground"
                :title="best.bookTitle ?? undefined">
                {{ suggestionDetail(best) }}
            </p>
        </div>
        <div class="flex items-center gap-2">
            <Button
                type="button"
                class="h-11 flex-1"
                :disabled="busy"
                @click="accept(best)">
                <LoaderCircle
                    v-if="linking === suggestionKey(best)"
                    class="animate-spin"
                    aria-hidden="true" />
                Link it
            </Button>
            <Button
                type="button"
                variant="outline"
                class="h-11 flex-1"
                :disabled="busy"
                @click="refuse(best)">
                No
            </Button>
        </div>

        <!-- The other candidates, behind one toggle: the best match is one decision, and the rest
             are there for when it is not the right one. -->
        <template v-if="others.length > 0">
            <button
                type="button"
                class="flex min-h-11 items-center gap-1 self-start text-xs text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                :aria-expanded="expanded"
                @click="expanded = !expanded">
                <ChevronDown
                    class="size-3.5 transition-transform"
                    :class="{ 'rotate-180': expanded }"
                    aria-hidden="true" />
                {{ otherMatchesLabel(others.length) }}
            </button>
            <ul
                v-if="expanded"
                class="flex flex-col gap-3 border-t pt-2">
                <li
                    v-for="item in others"
                    :key="suggestionKey(item)"
                    class="flex flex-col gap-2">
                    <div class="flex min-w-0 flex-col gap-0.5">
                        <p class="text-sm">{{ suggestionQuestion(item) }}</p>
                        <p
                            class="text-xs text-muted-foreground"
                            :title="item.bookTitle ?? undefined">
                            {{ suggestionDetail(item) }}
                        </p>
                    </div>
                    <div class="flex items-center gap-2">
                        <Button
                            type="button"
                            variant="outline"
                            class="h-11 flex-1"
                            :disabled="busy"
                            @click="accept(item)">
                            <LoaderCircle
                                v-if="linking === suggestionKey(item)"
                                class="animate-spin"
                                aria-hidden="true" />
                            Link it
                        </Button>
                        <Button
                            type="button"
                            variant="ghost"
                            class="h-11 flex-1"
                            :disabled="busy"
                            @click="refuse(item)">
                            No
                        </Button>
                    </div>
                </li>
            </ul>
        </template>
    </section>
</template>

<script setup lang="ts">
    import { ChevronDown, LoaderCircle } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { Entry, KnowledgeBaseItem } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import type { EntryViewer } from "~/utils/entries";
    import {
        otherMatchesLabel,
        suggestionDetail,
        suggestionKey,
        suggestionQuestion,
    } from "~/utils/kbSuggestions";

    const props = defineProps<{
        campaignId: string;
        entry: Entry;
        viewer: EntryViewer;
        /** The desktop's right-hand panel (25f): the card gets the panel's own margins. */
        panel?: boolean;
    }>();

    const id = useId();
    const suggestions = useKnowledgeBaseSuggestions(
        () => props.campaignId,
        () => props.entry,
        () => props.viewer
    );
    const { best, others, busy } = suggestions;

    // Which row's "Link it" is in flight, so only that button spins.
    const linking = ref<string | null>(null);
    const expanded = ref(false);
    // A different entry, or one whose prompt has gone, starts collapsed again.
    watch(
        () => props.entry.id,
        () => (expanded.value = false)
    );
    watch(others, (rest) => {
        if (rest.length === 0) expanded.value = false;
    });

    async function accept(item: KnowledgeBaseItem) {
        if (busy.value) return;
        linking.value = suggestionKey(item);
        try {
            await suggestions.linkIt(item);
        } catch (error) {
            // The optimistic row is already gone again: the link mutation rolls itself back.
            toast.error(apiErrorMessage(error, `Could not link ${item.name}.`));
        } finally {
            linking.value = null;
        }
    }

    async function refuse(item: KnowledgeBaseItem) {
        if (busy.value) return;
        try {
            await suggestions.dismissIt(item);
        } catch (error) {
            // The card is already back: the dismissal rolls its optimistic removal back.
            toast.error(apiErrorMessage(error, "Could not dismiss the suggestion."));
        }
    }
</script>
