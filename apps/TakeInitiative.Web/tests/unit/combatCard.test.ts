import { describe, expect, it } from "vitest";
import type { Combat, CombatCard, Combatant, SessionNote } from "~/utils/api/types";
import {
    cardCountPrefix,
    cardFromCombat,
    cardRoundsLabel,
    liveCombatBanner,
    mergeStreamItems,
} from "~/utils/combatCard";

const combatant = (id: string, extra: Partial<Combatant> = {}): Combatant => ({
    id,
    name: id,
    waiting: false,
    initiative: 10,
    hidden: false,
    playersSee: "Band",
    conditions: [],
    ...extra,
});

const combat = (combatants: Combatant[], extra: Partial<Combat> = {}): Combat => ({
    id: "c1",
    campaignId: "camp",
    sessionId: "s1",
    name: "Goblin Ambush",
    status: "Active",
    round: 3,
    createdAt: "2026-09-01T10:00:00Z",
    startedAt: "2026-09-01T10:05:00Z",
    combatants,
    ...extra,
});

describe("cardFromCombat", () => {
    it("groups combatants by entry, keeps plain names, in the combat's order", () => {
        const card = cardFromCombat(
            combat([
                combatant("k", { name: "Klarg", entryId: "E-KLARG" }),
                combatant("g1", { name: "Goblin 1", entryId: "e-goblin" }),
                combatant("b", { name: "Bandit" }),
                combatant("g2", { name: "Goblin 2", entryId: "e-goblin" }),
                combatant("g3", { name: "Goblin 3", entryId: "E-GOBLIN" }),
                combatant("b2", { name: "Bandit" }),
            ])
        );
        expect(card.combatants).toEqual([
            { name: "Klarg", entryId: "E-KLARG", count: 1 },
            { name: "Goblin", entryId: "e-goblin", count: 3 },
            { name: "Bandit", count: 1 },
            { name: "Bandit", count: 1 },
        ]);
        expect(card).toMatchObject({ id: "c1", sessionId: "s1", name: "Goblin Ambush", status: "Active", round: 3 });
    });

    it("keeps a single combatant's name as it is", () => {
        const card = cardFromCombat(combat([combatant("g", { name: "Goblin 2", entryId: "e" })]));
        expect(card.combatants).toEqual([{ name: "Goblin 2", entryId: "e", count: 1 }]);
    });

    it("carries only what the view carries: a redacted combatant is not there", () => {
        expect(cardFromCombat(combat([])).combatants).toEqual([]);
    });
});

describe("the stream merge", () => {
    const note = (id: string, postedAt: string) => ({ id, postedAt }) as SessionNote;
    const card = (id: string, createdAt: string, startedAt: string | null = null) =>
        ({ id, createdAt, startedAt }) as CombatCard;

    it("merges notes and cards by time, a started card by startedAt", () => {
        const items = mergeStreamItems(
            [note("n1", "2026-09-01T10:00:00Z"), note("n2", "2026-09-01T10:30:00Z"), note("n3", "2026-09-01T11:00:00Z")],
            [card("draft-then-run", "2026-09-01T09:00:00Z", "2026-09-01T10:40:00Z"), card("later", "2026-09-01T12:00:00Z")]
        );
        expect(items.map((i) => (i.kind === "note" ? i.note.id : i.card.id))).toEqual([
            "n1",
            "n2",
            "draft-then-run",
            "n3",
            "later",
        ]);
    });

    it("puts a note before a card at the same time, and handles either side empty", () => {
        const at = "2026-09-01T10:00:00Z";
        expect(mergeStreamItems([note("n", at)], [card("c", at)]).map((i) => i.kind)).toEqual(["note", "combat"]);
        expect(mergeStreamItems([], [card("c", at)]).map((i) => i.kind)).toEqual(["combat"]);
        expect(mergeStreamItems([note("n", at)], [])).toHaveLength(1);
    });
});

describe("card labels", () => {
    it("says the state and the rounds", () => {
        expect(cardRoundsLabel({ status: "Draft", round: 0 })).toBe("Draft");
        expect(cardRoundsLabel({ status: "Active", round: 2 })).toBe("Round 2");
        expect(cardRoundsLabel({ status: "Finished", round: 1 })).toBe("1 round");
        expect(cardRoundsLabel({ status: "Finished", round: 3 })).toBe("3 rounds");
        expect(cardRoundsLabel({ status: "Finished", round: 0 })).toBe("Never started");
        expect(cardCountPrefix({ count: 4 })).toBe("4× ");
        expect(cardCountPrefix({ count: 1 })).toBe("");
    });
});

describe("the Join combat banner", () => {
    const summary = (id: string, status: string, startedAt: string | null) => ({
        id,
        status,
        createdAt: "2026-09-01T09:00:00Z",
        startedAt,
    });

    it("names the newest live combat and counts the rest", () => {
        const banner = liveCombatBanner([
            summary("old", "Active", "2026-09-01T10:00:00Z"),
            summary("done", "Finished", "2026-09-01T12:00:00Z"),
            summary("new", "Active", "2026-09-01T11:00:00Z"),
        ]);
        expect(banner?.combat.id).toBe("new");
        expect(banner?.more).toBe(1);
    });

    it("is nothing with no live combat", () => {
        expect(liveCombatBanner([summary("done", "Finished", null)])).toBeNull();
        expect(liveCombatBanner(undefined)).toBeNull();
    });
});
