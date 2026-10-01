import { describe, expect, it } from "vitest";
import type { EntryChange, EntrySummary, KnowledgeBaseItem } from "~/utils/api/types";
import { describeChange, type EntryViewer } from "~/utils/entries";
import {
    SUGGESTIONS_MAX,
    canSeeSuggestions,
    canSuggestForKind,
    isSameSuggestion,
    otherMatchesLabel,
    splitSuggestions,
    suggestibleCategories,
    suggestionBadge,
    suggestionBadgeHint,
    suggestionDetail,
    suggestionKey,
    suggestionQuestion,
} from "~/utils/kbSuggestions";

// The knowledge-base prompt's pure rules (28c). No model is involved anywhere here: these are the
// client's copy of a SQL query's rules, the question's wording, and the collapsed header's badge.

const DM: EntryViewer = { memberId: "dm", isDm: true };
const PLAYER: EntryViewer = { memberId: "player", isDm: false };

type Subject = Pick<EntrySummary, "kind" | "claimedByMemberId" | "creatorMemberId" | "editAccess">;

const entry = (extra: Partial<Subject> = {}): Subject => ({
    kind: "Character",
    claimedByMemberId: null,
    creatorMemberId: "dm",
    editAccess: "Anyone",
    ...extra,
});

const item = (extra: Partial<KnowledgeBaseItem> = {}): KnowledgeBaseItem => ({
    provider: "5etools",
    providerLabel: "5eTools",
    id: "monster_beholder_mm",
    name: "Beholder",
    category: "Monster",
    label: "CR 13 · Large Aberration",
    book: "MM",
    bookTitle: "Monster Manual (2014)",
    page: 28,
    url: "https://5e.tools/bestiary.html#beholder_mm",
    ...extra,
});

describe("which kinds can be suggested anything", () => {
    it("matches a Character against monsters and an Item against items and spells", () => {
        expect(suggestibleCategories("Character")).toEqual(["Monster"]);
        expect(suggestibleCategories("Item")).toEqual(["Item", "Spell"]);
    });

    it("matches Other against spells, because a spell entry has no kind of its own", () => {
        // KnowledgeBaseItemRow.Summary files a Spell row under Other, so this is the inverse of what
        // + Wiki creates. Without it a corpus of spells could never be suggested at all.
        expect(suggestibleCategories("Other")).toEqual(["Spell"]);
    });

    it("matches a Place, a Faction and an Event against nothing", () => {
        for (const kind of ["Place", "Faction", "Event"] as const) {
            expect(suggestibleCategories(kind)).toEqual([]);
            expect(canSuggestForKind(kind)).toBe(false);
        }
    });
});

describe("who is asked", () => {
    it("asks the DM about an unclaimed Character and not the players", () => {
        // The prompt names the monster, which is the leak the links read rule exists to stop.
        expect(canSeeSuggestions(entry(), DM)).toBe(true);
        expect(canSeeSuggestions(entry(), PLAYER)).toBe(false);
    });

    it("asks a claimed Character's player, who may both read and write its links", () => {
        expect(canSeeSuggestions(entry({ claimedByMemberId: "player" }), PLAYER)).toBe(true);
    });

    it("does not ask a member who may read but not write", () => {
        // An Item's links are read by everyone who sees it, so this is the write half on its own: a
        // question whose only available answer is "No" is noise.
        const locked = entry({ kind: "Item", editAccess: "OnlyMe" });
        expect(canSeeSuggestions(locked, PLAYER)).toBe(false);
        expect(canSeeSuggestions(locked, DM)).toBe(true);
    });

    it("never asks about a kind that can match nothing, whoever is looking", () => {
        expect(canSeeSuggestions(entry({ kind: "Place" }), DM)).toBe(false);
    });
});

describe("the question and its detail line", () => {
    it("names the row and its book", () => {
        expect(suggestionQuestion(item())).toBe("Is this the Beholder from the Monster Manual (2014)?");
    });

    it("falls back to the book's abbreviation, and then to no book at all", () => {
        expect(suggestionQuestion(item({ bookTitle: null }))).toBe("Is this the Beholder from the MM?");
        expect(suggestionQuestion(item({ bookTitle: null, book: "" }))).toBe("Is this the Beholder?");
    });

    it("shows the row's own words and no score", () => {
        expect(suggestionDetail(item())).toBe("Monster · CR 13 · Large Aberration · MM · p. 28");
        expect(suggestionDetail(item({ label: null, page: null }))).toBe("Monster · MM");
    });
});

describe("the card's shape", () => {
    it("shows the best candidate and puts the rest behind one toggle", () => {
        const { best, others } = splitSuggestions(["a", "b", "c"]);
        expect(best).toBe("a");
        expect(others).toEqual(["b", "c"]);
    });

    it("renders nothing at all for an empty response", () => {
        const { best, others } = splitSuggestions([]);
        expect(best).toBeNull();
        expect(others).toEqual([]);
    });

    it("counts the others in the toggle's label", () => {
        expect(otherMatchesLabel(1)).toBe("1 other possible match");
        expect(otherMatchesLabel(2)).toBe("2 other possible matches");
    });

    it("offers at most three candidates, as the API caps them", () => {
        expect(SUGGESTIONS_MAX).toBe(3);
    });
});

describe("the collapsed header's badge", () => {
    it("is the count, and nothing when there is nothing to ask", () => {
        expect(suggestionBadge(2)).toBe("✨ 2");
        expect(suggestionBadge(0)).toBeNull();
    });

    it("is read out in words", () => {
        expect(suggestionBadgeHint(1)).toBe("1 suggested reference match");
        expect(suggestionBadgeHint(3)).toBe("3 suggested reference matches");
    });
});

describe("a row's identity", () => {
    it("folds the provider and not the id, as the API compares them", () => {
        expect(suggestionKey(item())).toBe("5etools:monster_beholder_mm");
        expect(isSameSuggestion(item(), item({ provider: "5eTOOLS" }))).toBe(true);
        expect(isSameSuggestion(item(), item({ id: "monster_beholder_zombie_mm" }))).toBe(false);
    });
});

describe("the history line", () => {
    const change = (extra: Partial<EntryChange> = {}): EntryChange => ({
        type: "SuggestionDismissed",
        suggestion: { provider: "5etools", providerLabel: "5eTools", itemId: "monster_beholder_mm", name: "Beholder", detail: "Monster · CR 13 · MM" },
        ...extra,
    });

    it("names the row and its provider", () => {
        expect(describeChange(change(), () => "Sam")).toBe("dismissed a suggestion: Beholder (5eTools)");
    });

    it("falls back to the item id for a row that has gone from the corpus", () => {
        expect(
            describeChange(change({ suggestion: { provider: "5etools", providerLabel: null, itemId: "monster_gone_mm", name: null, detail: null } }), () => "Sam")
        ).toBe("dismissed a suggestion: monster_gone_mm");
    });

    it("says nothing more than it knows", () => {
        expect(describeChange(change({ suggestion: null }), () => "Sam")).toBe("dismissed a suggestion");
    });
});
