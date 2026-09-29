import { describe, expect, it } from "vitest";
import type { EntryConnection, EntryKind } from "~/utils/api/types";
import {
    CONNECTIONS_SHOWN,
    connectionAriaLabel,
    foughtTogether,
    groupConnections,
    limitConnectionGroups,
    connectionStrip,
    CONNECTIONS_STRIP_SHOWN,
    noteEvidenceLabel,
    sortConnections,
} from "~/utils/connections";

let seq = 0;
function connection(
    name: string,
    kind: EntryKind,
    weight: number,
    { combats = 0, lastAt = null as string | null } = {}
): EntryConnection {
    seq++;
    return {
        entry: {
            id: `00000000-0000-0000-0000-${String(seq).padStart(12, "0")}`,
            name,
            kind,
            aliases: [],
            visibility: "Everyone",
            editAccess: "Anyone",
            creatorMemberId: "m1",
            createdAt: "2026-09-01T00:00:00Z",
            updatedAt: "2026-09-01T00:00:00Z",
            mergedFromIds: [],
        },
        weight,
        notes: weight - combats,
        blocks: 0,
        combats,
        lastAt,
    };
}

const names = (list: readonly EntryConnection[]) =>
    list.map((c) => c.entry.name);

describe("sortConnections", () => {
    it("puts the heaviest first, then the newest evidence, then the name", () => {
        const sorted = sortConnections([
            connection("Zed", "Character", 1, {
                lastAt: "2026-09-02T00:00:00Z",
            }),
            connection("Tharden", "Character", 3),
            connection("amber", "Item", 1, { lastAt: "2026-09-02T00:00:00Z" }),
            connection("Phandalin", "Place", 1, {
                lastAt: "2026-09-05T00:00:00Z",
            }),
            connection("Blocks only", "Other", 1),
        ]);
        expect(names(sorted)).toEqual([
            "Tharden",
            "Phandalin",
            "amber",
            "Zed",
            "Blocks only",
        ]);
    });
});

describe("groupConnections", () => {
    const tharden = connection("Tharden", "Character", 3);
    const phandalin = connection("Phandalin", "Place", 2);
    const cragmaw = connection("Cragmaw Hideout", "Place", 4);
    const redbrands = connection("Redbrands", "Faction", 5);

    it("puts a Character's Places first under Seen at", () => {
        const groups = groupConnections("Character", [
            tharden,
            phandalin,
            redbrands,
            cragmaw,
        ]);
        expect(groups.map((g) => [g.label, names(g.connections)])).toEqual([
            ["Seen at", ["Cragmaw Hideout", "Phandalin"]],
            ["Also connected", ["Redbrands", "Tharden"]],
        ]);
        expect(groups[0]!.key).toBe("seenAt");
    });

    it("puts a Place's Characters first under Seen here", () => {
        const brynn = connection("Brynn", "Character", 1);
        const groups = groupConnections("Place", [
            cragmaw,
            brynn,
            tharden,
            redbrands,
        ]);
        expect(
            groups.map((g) => [g.key, g.label, names(g.connections)])
        ).toEqual([
            ["seenHere", "Seen here", ["Tharden", "Brynn"]],
            ["all", "Also connected", ["Redbrands", "Cragmaw Hideout"]],
        ]);
    });

    it("is one plain group for any other kind", () => {
        const groups = groupConnections("Faction", [
            tharden,
            phandalin,
            redbrands,
        ]);
        expect(groups).toHaveLength(1);
        expect(groups[0]!.label).toBeNull();
        expect(names(groups[0]!.connections)).toEqual([
            "Redbrands",
            "Tharden",
            "Phandalin",
        ]);
    });

    it("leaves the rest unheaded when nothing is Seen at, and drops empty groups", () => {
        expect(
            groupConnections("Character", [tharden, redbrands]).map((g) => [
                g.key,
                g.label,
            ])
        ).toEqual([["all", null]]);
        expect(
            groupConnections("Character", [phandalin]).map((g) => g.key)
        ).toEqual(["seenAt"]);
        expect(groupConnections("Character", [])).toEqual([]);
    });
});

describe("connectionStrip (25e)", () => {
    it("flattens the first six in panel order and counts the rest", () => {
        const places = Array.from({ length: 2 }, (_, i) =>
            connection(`Place ${i}`, "Place", 1)
        );
        const others = Array.from({ length: 6 }, (_, i) =>
            connection(`Item ${i}`, "Item", 30 - i)
        );
        const { connections, hidden } = connectionStrip(
            groupConnections("Character", [...others, ...places])
        );
        expect(CONNECTIONS_STRIP_SHOWN).toBe(6);
        expect(names(connections)).toEqual([
            "Place 0",
            "Place 1",
            "Item 0",
            "Item 1",
            "Item 2",
            "Item 3",
        ]);
        expect(hidden).toBe(2);
        expect(connectionStrip([])).toEqual({ connections: [], hidden: 0 });
    });
});

describe("limitConnectionGroups", () => {
    it("shows the first 12 in panel order and counts the rest", () => {
        const places = Array.from({ length: 5 }, (_, i) =>
            connection(`Place ${i}`, "Place", 20 - i)
        );
        const others = Array.from({ length: 10 }, (_, i) =>
            connection(`Item ${i}`, "Item", 30 - i)
        );
        const { groups, hidden } = limitConnectionGroups(
            groupConnections("Character", [...others, ...places])
        );
        expect(CONNECTIONS_SHOWN).toBe(12);
        expect(groups.map((g) => g.connections.length)).toEqual([5, 7]);
        expect(names(groups[1]!.connections).at(-1)).toBe("Item 6");
        expect(hidden).toBe(3);
    });

    it("drops a group that gets no chips, and hides nothing under the limit", () => {
        const seen = Array.from({ length: 3 }, (_, i) =>
            connection(`Place ${i}`, "Place", 5)
        );
        const rest = [connection("Tharden", "Character", 1)];
        const grouped = groupConnections("Character", [...seen, ...rest]);
        expect(limitConnectionGroups(grouped, 3)).toEqual({
            groups: [grouped[0]],
            hidden: 1,
        });
        expect(limitConnectionGroups(grouped).hidden).toBe(0);
        expect(limitConnectionGroups([], 12)).toEqual({
            groups: [],
            hidden: 0,
        });
    });
});

describe("the chip", () => {
    it("marks a connection with combat evidence as Fought together", () => {
        const goblin = connection("Goblin", "Character", 2, { combats: 1 });
        const tharden = connection("Tharden", "Character", 1);
        expect(foughtTogether(goblin)).toBe(true);
        expect(foughtTogether(tharden)).toBe(false);
        expect(connectionAriaLabel(goblin)).toBe(
            "Goblin, 2 pieces of evidence, fought together"
        );
        expect(connectionAriaLabel(tharden)).toBe(
            "Tharden, 1 piece of evidence"
        );
    });

    it("labels a note row with its session and author", () => {
        expect(
            noteEvidenceLabel(
                { sessionNumber: 14, authorMemberId: "m1" },
                () => "Sam"
            )
        ).toBe("S14 · Sam");
    });
});
