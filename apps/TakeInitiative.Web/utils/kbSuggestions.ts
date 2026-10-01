// The knowledge-base prompt's pure rules (28c): who is asked, which kinds can be asked about at
// all, the question's wording, and what the collapsed section's badge says.
// `components/Wiki/KnowledgeBaseSuggestion.vue` and `composables/useKnowledgeBaseSuggestions.ts`
// only draw and wire these.
//
// **No model is involved.** Step 23's ✨ comes from GLiNER running in this browser over free text;
// this ✨ is one SQL query over a name, answered by the server. Nothing here downloads, scores or
// records provenance, and a link made from a prompt is a plain member action — which is why there is
// no `Actor.Model` anywhere near it and nothing from `utils/extraction/` is imported.
import type { EntryKind, EntrySummary, KnowledgeBaseItem, ReferenceCategory } from "./api/types";
import { canAddLinks } from "./links";
import { rowLine, rowSource } from "./knowledgeBase";
import type { EntryViewer } from "./entries";

/** The API's `EntrySuggestions.MaxSuggestions`: the prompt offers at most three candidates. */
export const SUGGESTIONS_MAX = 3;

/**
 * Which corpus categories an entry of this kind could be, mirrored from the API's
 * `KnowledgeBaseSuggester.CategoriesFor` — a `Character` may be a Monster, an `Item` may be an Item
 * or a Spell, and `Other` may be a Spell, because a spell row has no entry kind of its own and
 * + Wiki files it under Other.
 *
 * `Place`, `Faction` and `Event` match **nothing**: the corpus holds monsters, spells and items, and
 * a place named after a spell is a coincidence. The server decides this too; the copy is here so a
 * Place never asks the question in the first place.
 */
export function suggestibleCategories(kind: EntryKind): ReferenceCategory[] {
    switch (kind) {
        case "Character":
            return ["Monster"];
        case "Item":
            return ["Item", "Spell"];
        case "Other":
            return ["Spell"];
        default:
            return [];
    }
}

/** Whether an entry of this kind can be suggested anything at all. */
export const canSuggestForKind = (kind: EntryKind) => suggestibleCategories(kind).length > 0;

/**
 * Who is asked: **exactly the members who could accept it** — they may read the entry's links and
 * write them, which is `canAddLinks` (the API's `EntrySuggestions.CanSee`). The read half stops the
 * disclosure, because "Is this the Beholder?" on an unclaimed Character names the monster as
 * completely as its stat block would; the write half stops a question whose only answer is "No".
 *
 * The API redacts as well, and answers `[]` rather than a 403, so this is only how the page decides
 * whether to ask at all.
 */
export const canSeeSuggestions = (
    entry: Pick<EntrySummary, "kind" | "claimedByMemberId" | "creatorMemberId" | "editAccess">,
    viewer: EntryViewer
) => canSuggestForKind(entry.kind) && canAddLinks(entry, viewer);

/**
 * The question, in the plan's words: "Is this the Beholder from the Monster Manual?". The book's full
 * title where the corpus has one, its abbreviation where it does not, and no "from" at all where
 * there is neither — never "from undefined".
 */
export function suggestionQuestion(item: Pick<KnowledgeBaseItem, "name" | "bookTitle" | "book">): string {
    const book = item.bookTitle?.trim() || item.book?.trim() || "";
    return book ? `Is this the ${item.name} from the ${book}?` : `Is this the ${item.name}?`;
}

/**
 * The muted line under the question: the row's own words, "Monster · CR 13 · MM · p. 28". It is
 * 26f's two row lines joined, and it is deliberately the whole of what the prompt claims — there is
 * no confidence score, because a trigram similarity is not a probability and showing one would imply
 * more than it means.
 */
export const suggestionDetail = (
    item: Pick<KnowledgeBaseItem, "category" | "label" | "book" | "page">
): string => [rowLine(item), rowSource(item)].filter(Boolean).join(" · ");

/**
 * The card's shape: the best candidate, and the rest behind one toggle. A prompt that showed three
 * questions at once would be three decisions; the others are there for the case where the best match
 * is not the right one.
 */
export function splitSuggestions<T>(items: readonly T[]): { best: T | null; others: T[] } {
    return { best: items[0] ?? null, others: items.slice(1) };
}

/** The toggle's label: "2 other possible matches", "1 other possible match". */
export const otherMatchesLabel = (count: number) =>
    `${count} other possible ${count === 1 ? "match" : "matches"}`;

/**
 * The badge on the collapsed "More about X" header: "✨ 2", or null when there is nothing to show.
 * Without it a prompt inside a collapsed section would never be seen — which is the only reason the
 * count leaves the card at all.
 */
export const suggestionBadge = (count: number) => (count > 0 ? `✨ ${count}` : null);

/** What the badge is read out as: "2 suggested reference matches". */
export const suggestionBadgeHint = (count: number) =>
    `${count} suggested reference ${count === 1 ? "match" : "matches"}`;

/** A row's key, and the pair `POST …/dismiss` names it by. */
export const suggestionKey = (item: Pick<KnowledgeBaseItem, "provider" | "id">) =>
    `${item.provider}:${item.id}`;

/** Whether two rows are the same one, as the API compares them: the provider folds, the id does not. */
export const isSameSuggestion = (
    a: Pick<KnowledgeBaseItem, "provider" | "id">,
    b: Pick<KnowledgeBaseItem, "provider" | "id">
) => a.provider.toLowerCase() === b.provider.toLowerCase() && a.id === b.id;
