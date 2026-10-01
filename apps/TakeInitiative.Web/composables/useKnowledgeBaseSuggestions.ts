import { useQuery } from "@tanstack/vue-query";
import type { Entry, KnowledgeBaseItem } from "~/utils/api/types";
import type { EntryViewer } from "~/utils/entries";
import { canSeeSuggestions, splitSuggestions } from "~/utils/kbSuggestions";
import { pendingKnowledgeBaseLink } from "~/utils/links";
import {
    dismissEntrySuggestionMutation,
    getEntryKnowledgeBaseSuggestionsQuery,
    postEntryLinkMutation,
} from "~/utils/queries/entries";

/**
 * The knowledge-base prompt's state and its two actions (28c): the candidates this viewer is offered,
 * **Link it**, and **No**.
 *
 * **Link it is 27c's endpoint and nothing else.** There is no "accept a suggestion" write: it posts an
 * ordinary knowledge-base link, as this member, and the link's own invalidation takes the prompt away
 * and puts the link line in its place. That is also the design decision behind it — a trigram match is
 * not a model assertion, so the link carries no provenance that would make 23e's "revert by model
 * version" mean less than it says.
 *
 * **No is optimistic and does not confirm.** It is cheap and reversible: the way back is the
 * knowledge-base picker, which still has the row. A failure rolls the card back and says so.
 *
 * The page and the Links section both call this for the same entry; one query key means one request,
 * so the "✨ N" badge and the card never disagree.
 */
export function useKnowledgeBaseSuggestions(
    campaignId: () => string,
    entry: () => Entry | undefined,
    viewer: () => EntryViewer
) {
    // The client's half of "who is asked" (`canSeeSuggestions`): a Place, and a player looking at an
    // NPC, send no request. The API answers `[]` for them anyway — this only saves the round trip.
    const asked = computed(() => {
        const loaded = entry();
        return !!loaded && canSeeSuggestions(loaded, viewer());
    });

    const query = useQuery(
        getEntryKnowledgeBaseSuggestionsQuery(
            campaignId,
            () => entry()?.id ?? "",
            () => asked.value
        )
    );

    const items = computed<KnowledgeBaseItem[]>(() => (asked.value ? (query.data.value?.items ?? []) : []));
    const count = computed(() => items.value.length);
    const best = computed(() => splitSuggestions(items.value).best);
    const others = computed(() => splitSuggestions(items.value).others);

    const link = postEntryLinkMutation();
    const dismiss = dismissEntrySuggestionMutation();
    const busy = computed(() => link.isPending.value || dismiss.isPending.value);

    /** Links one candidate as an ordinary knowledge-base link (27c), with its row shown at once. */
    async function linkIt(item: KnowledgeBaseItem) {
        const loaded = entry();
        if (!loaded || busy.value) return;
        await link.mutateAsync({
            campaignId: campaignId(),
            entryId: loaded.id,
            kind: "KnowledgeBase",
            provider: item.provider,
            itemId: item.id,
            optimistic: pendingKnowledgeBaseLink(item, viewer().memberId),
        });
    }

    /** "No": this entry is not that row, campaign-wide and for good (28b). */
    async function dismissIt(item: KnowledgeBaseItem) {
        const loaded = entry();
        if (!loaded || busy.value) return;
        await dismiss.mutateAsync({
            campaignId: campaignId(),
            entryId: loaded.id,
            provider: item.provider,
            itemId: item.id,
        });
    }

    return { asked, query, items, count, best, others, busy, linkIt, dismissIt };
}
