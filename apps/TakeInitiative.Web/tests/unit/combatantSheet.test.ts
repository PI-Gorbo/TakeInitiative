import { describe, expect, it } from "vitest";
import type { Combat, Combatant } from "~/utils/api/types";
import {
    CONDITIONS_MAX,
    addCondition,
    applyHpDelta,
    canOpenSheet,
    combatantEdit,
    combatantSheetFields,
    conditionSuggestions,
    dropIndexAt,
    dropPosition,
    moveByOne,
    parseWholeNumber,
    removeCondition,
    withCombatantEdit,
    withMaxHp,
    withMovedCombatant,
} from "~/utils/combat";

const combatant = (over: Partial<Combatant> = {}): Combatant => ({
    id: "c1",
    name: "Goblin 1",
    waiting: false,
    initiative: 12,
    hp: 7,
    maxHp: 7,
    ac: 15,
    hidden: false,
    playersSee: "Band",
    conditions: [],
    ...over,
});

const combat = (combatants: Combatant[], over: Partial<Combat> = {}): Combat =>
    ({
        id: "k1",
        campaignId: "camp",
        sessionId: "s1",
        name: "Ambush",
        status: "Active",
        round: 1,
        turnCombatantId: null,
        createdAt: "2026-09-28T00:00:00Z",
        combatants,
        ...over,
    }) as Combat;

const dm = { isDm: true, memberId: "dm" };
const player = { isDm: false, memberId: "p1" };

describe("applyHpDelta", () => {
    it("damages down to −999 and no further", () => {
        expect(applyHpDelta(10, 20, 4, "damage")).toBe(6);
        expect(applyHpDelta(3, 20, 10, "damage")).toBe(-7);
        expect(applyHpDelta(-990, 20, 50, "damage")).toBe(-999);
    });

    it("heals up to Max HP", () => {
        expect(applyHpDelta(10, 20, 4, "heal")).toBe(14);
        expect(applyHpDelta(18, 20, 10, "heal")).toBe(20);
        expect(applyHpDelta(-5, 20, 3, "heal")).toBe(-2);
    });

    it("leaves HP above Max HP where it is when healing", () => {
        expect(applyHpDelta(25, 20, 5, "heal")).toBe(25);
    });

    it("heals to 9,999 with no Max HP", () => {
        expect(applyHpDelta(9990, null, 50, "heal")).toBe(9999);
    });

    it("starts from Max HP, or 0, with no HP", () => {
        expect(applyHpDelta(null, 12, 5, "damage")).toBe(7);
        expect(applyHpDelta(null, null, 5, "damage")).toBe(-5);
    });

    it("takes the size of the amount, whatever its sign", () => {
        expect(applyHpDelta(10, 20, -4, "damage")).toBe(6);
    });
});

describe("parseWholeNumber", () => {
    it("reads whole numbers in range, and blank as none", () => {
        expect(parseWholeNumber(" 12 ", 0, 99)).toEqual({
            value: 12,
            error: null,
        });
        expect(parseWholeNumber("-3", -99, 99).value).toBe(-3);
        expect(parseWholeNumber("+3", -99, 99).value).toBe(3);
        expect(parseWholeNumber("", 0, 99)).toEqual({
            value: null,
            error: null,
        });
    });

    it("refuses fractions, words and numbers out of range", () => {
        expect(parseWholeNumber("1.5", 0, 99).error).not.toBeNull();
        expect(parseWholeNumber("ten", 0, 99).error).not.toBeNull();
        expect(parseWholeNumber("100", 0, 99).error).toBe("Between 0 and 99.");
    });
});

describe("conditions", () => {
    it("adds a condition, trimmed, with an optional note", () => {
        const { conditions, error } = addCondition(
            [],
            "  Poisoned ",
            " until dawn "
        );
        expect(error).toBeNull();
        expect(conditions).toEqual([{ label: "Poisoned", note: "until dawn" }]);
        expect(addCondition([], "Prone").conditions).toEqual([
            { label: "Prone", note: null },
        ]);
    });

    it("does not add the same label twice, but takes a new note", () => {
        const start = [{ label: "Poisoned", note: null }];
        expect(addCondition(start, "poisoned").conditions).toEqual(start);
        expect(addCondition(start, "POISONED", "DC 12").conditions).toEqual([
            { label: "Poisoned", note: "DC 12" },
        ]);
    });

    it("refuses a blank or long label, a long note and a 21st condition", () => {
        expect(addCondition([], "  ").error).not.toBeNull();
        expect(addCondition([], "x".repeat(41)).error).not.toBeNull();
        expect(addCondition([], "Hexed", "x".repeat(201)).error).not.toBeNull();
        const full = Array.from({ length: CONDITIONS_MAX }, (_, i) => ({
            label: `C${i}`,
            note: null,
        }));
        const result = addCondition(full, "Prone");
        expect(result.error).not.toBeNull();
        expect(result.conditions).toHaveLength(CONDITIONS_MAX);
    });

    it("removes by position", () => {
        const list = [
            { label: "Prone", note: null },
            { label: "Poisoned", note: null },
        ];
        expect(removeCondition(list, 0)).toEqual([
            { label: "Poisoned", note: null },
        ]);
        expect(list).toHaveLength(2);
    });

    it("suggests the 5e list, less what it has, by what was typed", () => {
        expect(conditionSuggestions("", [])).toContain("Concentrating");
        expect(conditionSuggestions("pr", [])).toEqual(["Prone"]);
        expect(
            conditionSuggestions("", [{ label: "prone", note: null }])
        ).not.toContain("Prone");
    });
});

describe("the edit", () => {
    it("is the combatant's whole editable state", () => {
        expect(
            combatantEdit(
                combatant({
                    conditions: [{ label: "Prone" }],
                    initiative: undefined,
                })
            )
        ).toEqual({
            name: "Goblin 1",
            initiative: null,
            hp: 7,
            maxHp: 7,
            ac: 15,
            hidden: false,
            playersSee: "Band",
            conditions: [{ label: "Prone", note: null }],
        });
    });

    it("lands on the row before the server answers", () => {
        const c = combatant();
        const next = { ...combatantEdit(c), hp: 3, initiative: null };
        const after = withCombatantEdit(combat([c]), "c1", next);
        expect(after.combatants[0]).toMatchObject({
            hp: 3,
            band: "Bloodied",
            waiting: true,
        });
    });

    it("fills HP when Max HP is set on a combatant with none", () => {
        const e = combatantEdit(combatant({ hp: null, maxHp: null }));
        expect(withMaxHp(e, 12)).toMatchObject({ hp: 12, maxHp: 12 });
        const hurt = combatantEdit(combatant({ hp: 3 }));
        expect(withMaxHp(hurt, 12)).toMatchObject({ hp: 3, maxHp: 12 });
    });
});

describe("reordering", () => {
    const ids = ["a", "b", "c", "d"];

    it("drops at the top, in the middle and at the end", () => {
        expect(dropPosition(ids, "c", 0)).toEqual({ afterId: null });
        expect(dropPosition(ids, "a", 2)).toEqual({ afterId: "c" });
        expect(dropPosition(ids, "a", 3)).toEqual({ afterId: "d" });
        expect(dropPosition(ids, "d", 1)).toEqual({ afterId: "a" });
    });

    it("is null where it already was, or for an unknown id", () => {
        expect(dropPosition(ids, "b", 1)).toBeNull();
        expect(dropPosition(ids, "d", 3)).toBeNull();
        expect(dropPosition(ids, "z", 0)).toBeNull();
    });

    it("clamps an index past either end", () => {
        expect(dropPosition(ids, "b", 99)).toEqual({ afterId: "d" });
        expect(dropPosition(ids, "b", -3)).toEqual({ afterId: null });
    });

    it("moves up and down one place, and not past the ends", () => {
        expect(moveByOne(ids, "b", -1)).toEqual({ afterId: null });
        expect(moveByOne(ids, "b", 1)).toEqual({ afterId: "c" });
        expect(moveByOne(ids, "a", -1)).toBeNull();
        expect(moveByOne(ids, "d", 1)).toBeNull();
    });

    it("finds the drop index from the rows' middles", () => {
        const middles = [20, 70, 120];
        expect(dropIndexAt(middles, 5)).toBe(0);
        expect(dropIndexAt(middles, 60)).toBe(1);
        expect(dropIndexAt(middles, 200)).toBe(3);
    });

    it("moves the row at once, and leaves an unknown target alone", () => {
        const k = combat(ids.map((id) => combatant({ id })));
        const order = (x: Combat) => x.combatants.map((c) => c.id);
        expect(order(withMovedCombatant(k, "c", null))).toEqual([
            "c",
            "a",
            "b",
            "d",
        ]);
        expect(order(withMovedCombatant(k, "a", "c"))).toEqual([
            "b",
            "c",
            "a",
            "d",
        ]);
        expect(order(withMovedCombatant(k, "a", "zz"))).toEqual(ids);
    });
});

describe("who sees what in the sheet", () => {
    const mine = combatant({ ownerMemberId: "P1" });
    const theirs = combatant({ ownerMemberId: "p2" });
    const monster = combatant();

    it("opens for a DM on any combatant, and for a player on their own only", () => {
        const k = combat([mine, theirs, monster]);
        expect(canOpenSheet(k, monster, dm)).toBe(true);
        expect(canOpenSheet(k, mine, player)).toBe(true);
        expect(canOpenSheet(k, theirs, player)).toBe(false);
        expect(canOpenSheet(k, monster, player)).toBe(false);
    });

    it("opens for nobody once the combat has finished", () => {
        const k = combat([mine], { status: "Finished" });
        expect(canOpenSheet(k, mine, dm)).toBe(false);
        expect(canOpenSheet(k, mine, player)).toBe(false);
    });

    it("gives a DM every field", () => {
        expect(combatantSheetFields(combat([monster]), monster, dm)).toEqual({
            hp: true,
            conditions: true,
            dmFields: true,
            initiative: true,
            move: true,
            remove: true,
        });
    });

    it("gives a DM no Move on a waiting combatant", () => {
        const waiting = combatant({ waiting: true, initiative: null });
        expect(combatantSheetFields(combat([waiting]), waiting, dm)?.move).toBe(
            false
        );
    });

    it("gives a player HP, conditions and, while it waits, initiative", () => {
        expect(combatantSheetFields(combat([mine]), mine, player)).toEqual({
            hp: true,
            conditions: true,
            dmFields: false,
            initiative: false,
            move: false,
            remove: false,
        });
        const waiting = { ...mine, waiting: true, initiative: null };
        expect(
            combatantSheetFields(combat([waiting]), waiting, player)?.initiative
        ).toBe(true);
        expect(combatantSheetFields(combat([theirs]), theirs, player)).toBe(
            null
        );
    });
});
