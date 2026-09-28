import { describe, expect, it } from "vitest";
import type { EntrySource, SearchReferenceHit, StatBlock } from "~/utils/api/types";
import {
    abilityRows,
    addToWikiConflict,
    addToWikiItemFromHit,
    addToWikiStatsLine,
    conflictMessage,
    defaultAddVisibility,
    shortStatsLine,
    sourceLineParts,
    sourceLink,
    sourceName,
    sourceStatsLabel,
    attributionParts,
    crLine,
    detailLines,
    hpLine,
    initiativeLine,
    modifier,
    referenceHitLine,
    referencePath,
    signed,
    skillsLine,
    speedLine,
    statBlockSections,
    textBlocks,
    textRuns,
    typeLine,
} from "~/utils/reference";

const MINUS = "−";

// The Goblin Warrior, as `monsters.json` has it (20a).
const goblin: StatBlock = {
    id: "goblin-warrior",
    name: "Goblin Warrior",
    category: "Monster",
    size: "Small",
    type: "Fey",
    alignment: "Chaotic Neutral",
    ac: 15,
    acNote: "natural armor",
    initiativeBonus: 2,
    hp: 10,
    hitDice: "3d6",
    speed: { walk: 30, fly: null, swim: null, climb: null, burrow: null, hover: false },
    abilities: { str: 8, dex: 15, con: 10, int: 10, wis: 8, cha: 8 },
    saves: { str: -1, dex: 2, con: 0, int: 0, wis: -1, cha: -1 },
    skills: { stealth: 6 },
    vulnerabilities: "",
    resistances: "",
    immunities: "",
    conditionImmunities: "",
    senses: "Darkvision 60 ft.; Passive Perception 9",
    languages: "Common, Goblin",
    cr: "1/4",
    xp: 50,
    pb: 2,
    traits: [],
    actions: [
        { kind: "BonusAction", name: "Nimble Escape", text: "The goblin takes the Disengage or Hide action." },
        { kind: "Action", name: "Scimitar", text: "Melee Attack Roll: +4, reach 5 ft. Hit: 5 (1d6 + 2) Slashing damage." },
        { kind: "Action", name: "Shortbow", text: "Ranged Attack Roll: +4, range 80/320 ft. Hit: 5 (1d6 + 2) Piercing damage." },
    ],
};

describe("numbers", () => {
    it("writes modifiers with a sign, and a real minus", () => {
        expect(modifier(1)).toBe(`${MINUS}5`);
        expect(modifier(8)).toBe(`${MINUS}1`);
        expect(modifier(10)).toBe("+0");
        expect(modifier(11)).toBe("+0");
        expect(modifier(15)).toBe("+2");
        expect(modifier(30)).toBe("+10");
        expect(signed(0)).toBe("+0");
    });

    it("writes 5.2's initiative: the bonus, then 10 + the bonus", () => {
        expect(initiativeLine(2)).toBe("+2 (12)");
        expect(initiativeLine(0)).toBe("+0 (10)");
        expect(initiativeLine(-1)).toBe(`${MINUS}1 (9)`);
    });

    it("builds the ability table's MOD and SAVE columns", () => {
        const rows = abilityRows(goblin);
        expect(rows.map((r) => r.label)).toEqual(["STR", "DEX", "CON", "INT", "WIS", "CHA"]);
        expect(rows[1]).toEqual({ key: "dex", label: "DEX", score: 15, mod: "+2", save: "+2" });
        expect(rows[0]).toMatchObject({ mod: `${MINUS}1`, save: `${MINUS}1` });
    });
});

describe("header lines", () => {
    it("writes the type line", () => {
        expect(typeLine(goblin)).toBe("Small Fey, Chaotic Neutral");
        expect(typeLine({ size: "Huge", type: "Dragon", alignment: "" })).toBe("Huge Dragon");
    });

    it("writes HP with its hit dice", () => {
        expect(hpLine(goblin)).toBe("10 (3d6)");
    });

    it("writes the speed line: walking, then the others A–Z, with hover after Fly", () => {
        expect(speedLine(goblin.speed)).toBe("30 ft.");
        expect(speedLine({ walk: 10, fly: 90, swim: 30, climb: 20, burrow: 5, hover: true })).toBe(
            "10 ft., Burrow 5 ft., Climb 20 ft., Fly 90 ft. (hover), Swim 30 ft."
        );
        expect(speedLine({ walk: 0, fly: 50, swim: null, climb: null, burrow: null, hover: true })).toBe(
            "0 ft., Fly 50 ft. (hover)"
        );
    });

    it("writes skills A–Z with their names", () => {
        expect(skillsLine({ stealth: 6, perception: 4, sleightOfHand: -1 })).toBe(
            `Perception +4, Sleight of Hand ${MINUS}1, Stealth +6`
        );
    });

    it("writes CR with XP and PB", () => {
        expect(crLine(goblin)).toBe("1/4 (XP 50; PB +2)");
        expect(crLine({ cr: "21", xp: 33000, pb: 7 })).toBe("21 (XP 33,000; PB +7)");
    });

    it("keeps only the lines that are set, with Languages None", () => {
        expect(detailLines(goblin).map((l) => l.label)).toEqual(["Skills", "Senses", "Languages"]);
        const lines = detailLines({
            ...goblin,
            skills: {},
            immunities: "Poison",
            conditionImmunities: "Charmed, Poisoned",
            languages: "",
        });
        expect(lines).toContainEqual({ label: "Immunities", text: "Poison; Charmed, Poisoned" });
        expect(lines).toContainEqual({ label: "Languages", text: "None" });
    });
});

describe("statBlockSections", () => {
    it("groups actions by kind, in 5.2's order, keeping the data's order within each", () => {
        const sections = statBlockSections({
            ...goblin,
            traits: [{ name: "Pack Tactics", text: "…" }],
            actions: [
                ...goblin.actions,
                { kind: "LegendaryAction", name: "Pounce", text: "…" },
                { kind: "Reaction", name: "Redirect Attack", text: "…" },
            ],
        });
        expect(sections.map((s) => s.title)).toEqual([
            "Traits",
            "Actions",
            "Bonus Actions",
            "Reactions",
            "Legendary Actions",
        ]);
        expect(sections[1].rows.map((r) => r.name)).toEqual(["Scimitar", "Shortbow"]);
    });

    it("leaves out sections with no rows", () => {
        expect(statBlockSections(goblin).map((s) => s.title)).toEqual(["Actions", "Bonus Actions"]);
    });
});

describe("rules text", () => {
    it("splits _italic_ runs out as text", () => {
        expect(textRuns("_Trigger:_ A creature attacks. _Response:_ The goblin swaps.")).toEqual([
            { text: "Trigger:", italic: true },
            { text: " A creature attacks. " },
            { text: "Response:", italic: true },
            { text: " The goblin swaps." },
        ]);
    });

    it("splits **bold** runs out, and leaves everything else as text", () => {
        expect(textRuns("uses **Antennae**.")).toEqual([
            { text: "uses " },
            { text: "Antennae", bold: true },
            { text: "." },
        ]);
        expect(textRuns("<b>not markup</b> & 2 * 3")).toEqual([{ text: "<b>not markup</b> & 2 * 3" }]);
    });

    it("reads blank-line paragraphs and - lists", () => {
        const blocks = textBlocks(
            "The vampire has these weaknesses:\n\n- **Forbiddance:** No entry.\n- **Sunlight:** 20 Radiant damage."
        );
        expect(blocks).toEqual([
            { type: "paragraph", runs: [{ text: "The vampire has these weaknesses:" }] },
            {
                type: "list",
                items: [
                    [{ text: "Forbiddance:", bold: true }, { text: " No entry." }],
                    [{ text: "Sunlight:", bold: true }, { text: " 20 Radiant damage." }],
                ],
            },
        ]);
    });

    it("reads a list straight after a line, with no blank line", () => {
        const blocks = textBlocks("The dragon casts:\n- **At Will:** Mind Spike\n- **1/Day Each:** Geas");
        expect(blocks.map((b) => b.type)).toEqual(["paragraph", "list"]);
    });

    it("keeps a plain text as one paragraph", () => {
        expect(textBlocks("The goblin takes the Disengage or Hide action.")).toEqual([
            { type: "paragraph", runs: [{ text: "The goblin takes the Disengage or Hide action." }] },
        ]);
    });
});

describe("routes and rows", () => {
    it("builds the card's route", () => {
        expect(referencePath("c1", "srd52", "goblin-warrior")).toBe("/app/campaigns/c1/reference/srd52/goblin-warrior");
        expect(referencePath("c 1", "srd52", "a/b")).toBe("/app/campaigns/c%201/reference/srd52/a%2Fb");
    });

    it("writes a ⌘K row's line", () => {
        expect(
            referenceHitLine({ category: "Monster", detail: "CR 1/4 · Small Fey", providerLabel: "SRD 5.2" })
        ).toBe("Monster · CR 1/4 · SRD 5.2");
    });

    it("writes a 5eTools row's line with its book (21c)", () => {
        const row = (category: SearchReferenceHit["category"], detail: string) =>
            referenceHitLine({ category, detail, providerLabel: "5eTools", hasStatBlock: false });
        expect(row("Monster", "CR 13 · Large Aberration · MM")).toBe("Monster · CR 13 · MM (5eTools)");
        expect(row("Monster", "Large Aberration · MM")).toBe("Monster · MM (5eTools)");
        expect(row("Spell", "Level 3 Evocation · XPHB")).toBe("Spell · Level 3 · XPHB (5eTools)");
        expect(row("Spell", "Illusion Cantrip · PHB")).toBe("Spell · Cantrip · PHB (5eTools)");
        expect(row("Item", "Uncommon Wondrous Item · XDMG")).toBe("Item · Uncommon Wondrous Item · XDMG (5eTools)");
        // A row with no label: the index's detail is just the book.
        expect(row("Item", "XPHB")).toBe("Item · XPHB (5eTools)");
    });
});

describe("attributionParts", () => {
    it("links the URLs, without the full stop after them", () => {
        const parts = attributionParts(
            "Material from the SRD, available at https://www.dndbeyond.com/srd. Licensed at https://creativecommons.org/licenses/by/4.0/legalcode."
        );
        expect(parts.filter((p) => p.href).map((p) => p.href)).toEqual([
            "https://www.dndbeyond.com/srd",
            "https://creativecommons.org/licenses/by/4.0/legalcode",
        ]);
        expect(parts.map((p) => p.text).join("")).toBe(
            "Material from the SRD, available at https://www.dndbeyond.com/srd. Licensed at https://creativecommons.org/licenses/by/4.0/legalcode."
        );
    });
});

describe("+ Wiki", () => {
    const hit: SearchReferenceHit = {
        provider: "srd52",
        providerLabel: "SRD 5.2",
        id: "goblin-warrior",
        name: "Goblin Warrior",
        category: "Monster",
        detail: "CR 1/4 · Small Fey",
        url: null,
        hasStatBlock: true,
        suggestedKind: "Character",
    };
    const stats = { initiativeRoll: "1d20+2", maxHp: "3d6", ac: 15 };

    it("defaults to DM for a DM and Everyone for a player", () => {
        expect(defaultAddVisibility(true)).toBe("DM");
        expect(defaultAddVisibility(false)).toBe("Everyone");
    });

    it("takes a ⌘K hit without Stats, so the dialog reads them", () => {
        const item = addToWikiItemFromHit(hit);
        expect(item).toMatchObject({ provider: "srd52", id: "goblin-warrior", name: "Goblin Warrior" });
        expect("stats" in item).toBe(false);
    });

    it("tells a DM the Stats and a player that a DM adds them", () => {
        expect(shortStatsLine(stats)).toBe("1d20+2 · 3d6 · AC 15");
        expect(addToWikiStatsLine("Character", stats, true)).toBe("Stats: 1d20+2 · 3d6 · AC 15");
        expect(addToWikiStatsLine("Character", stats, false)).toBe("A DM can add its stats.");
        expect(addToWikiStatsLine("Character", undefined, true)).toBeNull();
        expect(addToWikiStatsLine("Item", stats, true)).toBeNull();
        expect(shortStatsLine({ ac: 0 })).toBe("AC 0");
    });

    it("gives a 5eTools item the kind and Stats line of its category (21c)", () => {
        const fiveETools = { ...hit, provider: "5etools", providerLabel: "5eTools", hasStatBlock: false };
        const monster = addToWikiItemFromHit({ ...fiveETools, id: "monster_beholder_mm" });
        const spell = addToWikiItemFromHit({ ...fiveETools, id: "spell_fireball_xphb", category: "Spell", suggestedKind: "Other" });
        const item = addToWikiItemFromHit({ ...fiveETools, id: "item_bag_xdmg", category: "Item", suggestedKind: "Item" });
        expect([monster, spell, item].map((i) => i.suggestedKind)).toEqual(["Character", "Other", "Item"]);
        expect(addToWikiStatsLine(monster.suggestedKind, stats, true)).toBe("Stats: 1d20+2 · 3d6 · AC 15");
        expect(addToWikiStatsLine(spell.suggestedKind, stats, true)).toBeNull();
        expect(addToWikiStatsLine(item.suggestedKind, null, false)).toBeNull();
    });

    it("keeps a 409 while the name is the one that clashed", () => {
        const conflict = addToWikiConflict(409, " Goblin ", "e1");
        expect(conflict).toEqual({ name: "Goblin", existingId: "e1" });
        expect(conflictMessage(conflict, "Goblin")).toBe("There's already an entry called Goblin.");
        expect(conflictMessage(conflict, "goblin ")).toBe("There's already an entry called Goblin.");
        expect(conflictMessage(conflict, "Goblin Warrior")).toBeNull();
        expect(addToWikiConflict(400, "Goblin", null)).toBeNull();
        expect(addToWikiConflict(409, "Goblin", null)).toEqual({ name: "Goblin", existingId: null });
        expect(conflictMessage(null, "Goblin")).toBeNull();
    });
});

describe("the source line", () => {
    const source: EntrySource = {
        provider: "srd52",
        providerLabel: "SRD 5.2",
        externalId: "goblin-warrior",
        name: "Goblin Warrior",
        url: "https://www.dndbeyond.com/srd",
        hasStatBlock: true,
    };

    it("links to our card for a provider with stat blocks", () => {
        expect(sourceLink("c1", source)).toEqual({ to: "/app/campaigns/c1/reference/srd52/goblin-warrior" });
        expect(sourceName(source)).toBe("Goblin Warrior");
    });

    it("links out for a search-only provider", () => {
        const url = "https://5e.tools/bestiary.html#owlbear_xmm";
        expect(sourceLink("c1", { ...source, provider: "5etools", hasStatBlock: false, url })).toEqual({ href: url });
    });

    it("shows the stored id and no link when the item has gone", () => {
        const gone = { ...source, name: null, hasStatBlock: false };
        expect(sourceLink("c1", gone)).toBeNull();
        expect(sourceName(gone)).toBe("goblin-warrior");
    });

    it("reads \"↗ From 5eTools · Beholder (MM p. 28)\" for a search-only provider, titled with the book (21c)", () => {
        const url = "https://5e.tools/bestiary.html#beholder_mm";
        const beholder: EntrySource = {
            provider: "5etools",
            providerLabel: "5eTools",
            externalId: "monster_beholder_mm",
            name: "Beholder",
            url,
            detail: "MM p. 28",
            bookTitle: "Monster Manual (2014)",
            hasStatBlock: false,
        };
        expect(sourceLineParts(beholder)).toEqual({
            icon: "↗",
            from: "From 5eTools",
            name: "Beholder",
            book: "(MM p. 28)",
            bookTitle: "Monster Manual (2014)",
        });
        expect(sourceLink("c1", beholder)).toEqual({ href: url });
        // Gone from the index (or the index is off): the stored id, no book, no link.
        const gone = { ...beholder, name: null, detail: null, bookTitle: null };
        expect(sourceLineParts(gone)).toMatchObject({ icon: "📖", name: "monster_beholder_mm", book: null, bookTitle: null });
        expect(sourceLink("c1", gone)).toBeNull();
        // The SRD has no book.
        expect(sourceLineParts(source)).toMatchObject({ icon: "📖", from: "From SRD 5.2", book: null });
    });

    it("names the Stats button after the provider (21c)", () => {
        expect(sourceStatsLabel(source)).toBe("Use SRD 5.2 stats");
        expect(sourceStatsLabel({ providerLabel: "5eTools" })).toBe("Use 5eTools stats");
    });
});
