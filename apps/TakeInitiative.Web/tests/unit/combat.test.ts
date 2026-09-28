import { describe, expect, it } from "vitest";
import type {
    Combat,
    Combatant,
    CombatSummary,
    EntryList,
    EntrySummary,
} from "~/utils/api/types";
import {
    combatActions,
    combatantRequests,
    combatStatusLabel,
    combatSummaryLine,
    endTurnState,
    groupCombats,
    hpBand,
    hpView,
    myCharacterCandidates,
    parseAddCombatants,
    rollLabel,
    splitCombatants,
    stageCombatant,
    stagedTotal,
    summaryFromCombat,
    turnCombatant,
    upsertCombatSummary,
    type StagedCombatant,
} from "~/utils/combat";
import { entryDirectory } from "~/utils/entries";

// ── Fixtures ─────────────────────────────────────────────────────────────────

const DM = { memberId: "dm", isDm: true };
const BRYNN_PLAYER = { memberId: "p1", isDm: false };
const OTHER_PLAYER = { memberId: "p2", isDm: false };

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

const combat = (extra: Partial<Combat> = {}): Combat => ({
    id: "c1",
    campaignId: "camp",
    sessionId: "s1",
    name: "Goblin Ambush",
    status: "Active",
    round: 1,
    createdAt: "2026-09-01T10:00:00Z",
    startedAt: "2026-09-01T10:05:00Z",
    turnCombatantId: "goblin-1",
    combatants: [
        combatant("goblin-1", { name: "Goblin 1", initiative: 17 }),
        combatant("brynn", {
            name: "Brynn",
            initiative: 12,
            ownerMemberId: "p1",
            playersSee: "Exact",
        }),
        combatant("goblin-2", {
            name: "Goblin 2",
            initiative: null,
            waiting: true,
            initiativeRoll: "1d20+2",
        }),
    ],
    ...extra,
});

const summary = (
    id: string,
    extra: Partial<CombatSummary> = {}
): CombatSummary => ({
    id,
    name: id,
    status: "Active",
    round: 1,
    sessionId: "s1",
    sessionNumber: 3,
    combatantCount: 2,
    createdAt: "2026-09-01T10:00:00Z",
    ...extra,
});

const entry = (
    id: string,
    name: string,
    extra: Partial<EntrySummary> = {}
): EntrySummary => ({
    id,
    name,
    kind: "Character",
    aliases: [],
    visibility: "Everyone",
    editAccess: "Anyone",
    creatorMemberId: "dm",
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z",
    mergedFromIds: [],
    ...extra,
});
const directoryOf = (...entries: EntrySummary[]) =>
    entryDirectory({
        entries: entries.map((e) => ({ entry: e, mentionCount: 0 })),
    } as EntryList);

// ── parseAddCombatants ───────────────────────────────────────────────────────

describe("parseAddCombatants", () => {
    it.each([
        ["goblin ×4", "goblin", 4],
        ["goblin x4", "goblin", 4],
        ["goblin X 4", "goblin", 4],
        ["goblin×4", "goblin", 4],
        ["goblin *3", "goblin", 3],
        ["@Goblin ×4", "Goblin", 4],
        ["  @Goblin   ×   2  ", "Goblin", 2],
        ["Young Red Dragon x2", "Young Red Dragon", 2],
    ])("reads %j as %j ×%i", (text, query, count) => {
        expect(parseAddCombatants(text)).toEqual({
            query,
            count,
            hasCount: true,
        });
    });

    it("is a count of 1 with no count", () => {
        expect(parseAddCombatants("@Klarg")).toEqual({
            query: "Klarg",
            count: 1,
            hasCount: false,
        });
        expect(parseAddCombatants("Bandit")).toEqual({
            query: "Bandit",
            count: 1,
            hasCount: false,
        });
    });

    it("keeps an x that ends a name", () => {
        expect(parseAddCombatants("Hex4")).toEqual({
            query: "Hex4",
            count: 1,
            hasCount: false,
        });
        expect(parseAddCombatants("Onyx")).toMatchObject({
            query: "Onyx",
            count: 1,
        });
    });

    it("clamps counts to 1–20", () => {
        expect(parseAddCombatants("goblin ×0").count).toBe(1);
        expect(parseAddCombatants("goblin ×50").count).toBe(20);
        expect(parseAddCombatants("goblin x9999").count).toBe(20);
    });

    it("drops a × still waiting for its number", () => {
        expect(parseAddCombatants("goblin ×").query).toBe("goblin");
    });
});

// ── Staging ──────────────────────────────────────────────────────────────────

const staged = (
    extra: Partial<StagedCombatant> = {}
): Omit<StagedCombatant, "key"> => ({
    entryId: "goblin",
    name: "Goblin",
    count: 1,
    maxHp: "",
    ac: "",
    initiativeRoll: "",
    stats: "some",
    ...extra,
});

describe("staging combatants", () => {
    it("adds to an entry already staged, up to 20", () => {
        let list = stageCombatant([], staged({ count: 4 }), "a");
        list = stageCombatant(
            list,
            staged({ entryId: "GOBLIN", count: 3 }),
            "b"
        );
        expect(list).toHaveLength(1);
        expect(list[0]).toMatchObject({ key: "a", count: 7 });
        list = stageCombatant(list, staged({ count: 20 }), "c");
        expect(list[0]!.count).toBe(20);
    });

    it("keeps plain names apart", () => {
        let list = stageCombatant(
            [],
            staged({ entryId: null, name: "Bandit" }),
            "a"
        );
        list = stageCombatant(
            list,
            staged({ entryId: null, name: "Bandit" }),
            "b"
        );
        expect(list).toHaveLength(2);
        expect(stagedTotal(list)).toBe(2);
    });

    it("sends blank fields as the entry's defaults, and a plain name by name", () => {
        const { combatants, error } = combatantRequests([
            { ...staged({ count: 4, maxHp: " 2d6 ", ac: "15" }), key: "a" },
            {
                ...staged({
                    entryId: null,
                    name: "Bandit",
                    initiativeRoll: "1d20+1",
                }),
                key: "b",
            },
        ]);
        expect(error).toBeNull();
        expect(combatants).toEqual([
            { entryId: "goblin", count: 4, maxHp: "2d6", ac: 15 },
            { name: "Bandit", count: 1, initiativeRoll: "1d20+1" },
        ]);
    });

    it("rejects an AC that is not 0–99", () => {
        expect(
            combatantRequests([{ ...staged({ ac: "100" }), key: "a" }]).error
        ).toMatch(/AC/);
        expect(
            combatantRequests([{ ...staged({ ac: "-1" }), key: "a" }]).error
        ).toMatch(/AC/);
        expect(
            combatantRequests([{ ...staged({ ac: "0" }), key: "a" }]).error
        ).toBeNull();
    });
});

// ── The order and the turn ───────────────────────────────────────────────────

describe("the order", () => {
    it("splits the order from the waiting, keeping the server's order", () => {
        const { ordered, waiting } = splitCombatants(combat());
        expect(ordered.map((c) => c.id)).toEqual(["goblin-1", "brynn"]);
        expect(waiting.map((c) => c.id)).toEqual(["goblin-2"]);
    });

    it("finds the turn's combatant", () => {
        expect(turnCombatant(combat())?.name).toBe("Goblin 1");
        expect(
            turnCombatant(combat({ turnCombatantId: null }))
        ).toBeUndefined();
        // A hidden combatant's turn reaches a player as no turn.
        expect(
            turnCombatant(combat({ turnCombatantId: "not-in-view" }))
        ).toBeUndefined();
    });
});

// ── HP ───────────────────────────────────────────────────────────────────────

describe("HP", () => {
    it("bands by the API's rule", () => {
        expect(hpBand(15, 15)).toBe("Healthy");
        expect(hpBand(8, 15)).toBe("Healthy");
        expect(hpBand(7, 15)).toBe("Bloodied");
        expect(hpBand(0, 15)).toBe("Down");
        expect(hpBand(-3, 15)).toBe("Down");
        expect(hpBand(5, null)).toBeNull();
    });

    it("shows exact HP with a bar", () => {
        expect(hpView({ hp: 31, maxHp: 45 })).toEqual({
            kind: "exact",
            hp: 31,
            maxHp: 45,
            percent: 69,
            band: "Healthy",
        });
        expect(hpView({ hp: -2, maxHp: 10 })).toMatchObject({
            percent: 0,
            band: "Down",
        });
        expect(hpView({ hp: 5, maxHp: null })).toEqual({
            kind: "exact",
            hp: 5,
            maxHp: null,
            percent: null,
            band: null,
        });
    });

    it("shows a band chip for Band, and nothing for Nothing", () => {
        expect(hpView({ band: "Bloodied" })).toEqual({
            kind: "band",
            band: "Bloodied",
        });
        expect(hpView({})).toEqual({ kind: "none" });
    });
});

// ── Actions per role and status ──────────────────────────────────────────────

describe("actions", () => {
    it("gives a DM Add, Start combat and Finish on a Draft, and no bar", () => {
        const draft = combat({
            status: "Draft",
            round: 0,
            turnCombatantId: null,
            startedAt: null,
        });
        expect(combatActions(draft, DM)).toEqual({
            add: true,
            roll: "Start combat",
            finish: true,
            addMyCharacter: false,
            rollMine: false,
            endTurn: null,
        });
    });

    it("labels a DM's roll by the waiting count, and hides it with none waiting", () => {
        expect(rollLabel(combat())).toBe("Roll 1 waiting");
        const placed = combat({ combatants: [combatant("a"), combatant("b")] });
        expect(rollLabel(placed)).toBeNull();
    });

    it("lets a DM end anyone's turn", () => {
        expect(endTurnState(combat(), DM)).toEqual({
            enabled: true,
            label: "End Goblin 1's turn",
            yourTurn: false,
            combatantId: "goblin-1",
        });
    });

    it("lets a player end only their own turn", () => {
        const brynnsTurn = combat({ turnCombatantId: "brynn" });
        expect(endTurnState(brynnsTurn, BRYNN_PLAYER)).toMatchObject({
            enabled: true,
            yourTurn: true,
            label: "End your turn",
        });
        expect(endTurnState(brynnsTurn, OTHER_PLAYER)).toMatchObject({
            enabled: false,
            label: "Brynn's turn",
        });
        expect(
            endTurnState(combat({ turnCombatantId: null }), BRYNN_PLAYER)
        ).toMatchObject({ enabled: false });
    });

    it("gives a player Add my character and, while waiting, Roll my initiative, on an Active combat only", () => {
        const waiting = combat({
            combatants: [
                combatant("brynn", {
                    ownerMemberId: "p1",
                    waiting: true,
                    initiative: null,
                }),
            ],
        });
        expect(combatActions(waiting, BRYNN_PLAYER)).toMatchObject({
            add: false,
            roll: null,
            finish: false,
            addMyCharacter: true,
            rollMine: true,
        });
        expect(combatActions(waiting, OTHER_PLAYER).rollMine).toBe(false);
    });

    it("gives nobody anything on a Finished combat", () => {
        const done = combat({ status: "Finished", turnCombatantId: null });
        for (const viewer of [DM, BRYNN_PLAYER]) {
            expect(combatActions(done, viewer)).toEqual({
                add: false,
                roll: null,
                finish: false,
                addMyCharacter: false,
                rollMine: false,
                endTurn: null,
            });
        }
    });

    it("offers the player's claimed characters not in the combat yet", () => {
        const brynn = entry("brynn-entry", "Brynn", {
            claimedByMemberId: "p1",
        });
        const alt = entry("alt-entry", "Alt", {
            claimedByMemberId: "p1",
            mergedFromIds: ["old-alt"],
        });
        const theirs = entry("c-entry", "Cade", { claimedByMemberId: "p2" });
        const place = entry("place", "Phandalin", {
            kind: "Place",
            claimedByMemberId: "p1",
        });
        const directory = directoryOf(brynn, alt, theirs, place);
        const inCombat = combat({
            combatants: [combatant("x", { entryId: "old-alt" })],
        });
        expect(
            myCharacterCandidates(directory, inCombat, "p1").map((e) => e.name)
        ).toEqual(["Brynn"]);
        expect(
            myCharacterCandidates(directory, combat(), "p1").map((e) => e.name)
        ).toEqual(["Alt", "Brynn"]);
    });
});

// ── The list ─────────────────────────────────────────────────────────────────

describe("the list", () => {
    it("groups Live, Drafts for DMs only, and Finished, newest first", () => {
        const list = [
            summary("old", {
                status: "Finished",
                finishedAt: "2026-09-01T00:00:00Z",
            }),
            summary("new", {
                status: "Finished",
                finishedAt: "2026-09-02T00:00:00Z",
            }),
            summary("draft", { status: "Draft", round: 0 }),
            summary("live"),
        ];
        const dm = groupCombats(list, true);
        expect(dm.live.map((s) => s.id)).toEqual(["live"]);
        expect(dm.drafts.map((s) => s.id)).toEqual(["draft"]);
        expect(dm.finished.map((s) => s.id)).toEqual(["new", "old"]);
        expect(groupCombats(list, false).drafts).toEqual([]);
    });

    it("labels rows and statuses", () => {
        expect(
            combatSummaryLine(summary("a", { round: 3, combatantCount: 6 }))
        ).toBe("Round 3 · 6 combatants");
        expect(
            combatSummaryLine(
                summary("a", { status: "Draft", round: 0, combatantCount: 1 })
            )
        ).toBe("1 combatant");
        expect(
            combatSummaryLine(
                summary("a", {
                    status: "Finished",
                    round: 2,
                    combatantCount: 0,
                })
            )
        ).toBe("2 rounds · No combatants");
        expect(combatStatusLabel({ status: "Active", round: 3 })).toBe(
            "Round 3"
        );
        expect(combatStatusLabel({ status: "Draft", round: 0 })).toBe("Draft");
    });

    it("upserts a pushed summary, and drops one a filtered list does not want", () => {
        const list = {
            combats: [
                summary("a"),
                summary("b", { createdAt: "2026-09-02T00:00:00Z" }),
            ],
        };
        const changed = upsertCombatSummary(list, summary("a", { round: 2 }))!;
        expect(changed.combats.map((c) => [c.id, c.round])).toEqual([
            ["b", 1],
            ["a", 2],
        ]);
        const finished = upsertCombatSummary(
            list,
            summary("a", { status: "Finished" }),
            ["Active"]
        )!;
        expect(finished.combats.map((c) => c.id)).toEqual(["b"]);
        expect(upsertCombatSummary(undefined, summary("a"))).toBeUndefined();
    });

    it("builds a write's summary from the listed one, unless the session moved", () => {
        const listed = summary("c1", {
            sessionNumber: 7,
            combatantCount: 0,
            status: "Draft",
            round: 0,
        });
        expect(summaryFromCombat(combat(), listed)).toMatchObject({
            sessionNumber: 7,
            status: "Active",
            round: 1,
            combatantCount: 3,
        });
        expect(
            summaryFromCombat(combat({ sessionId: "s2" }), listed)
        ).toBeNull();
        expect(summaryFromCombat(combat(), undefined)).toBeNull();
    });
});
