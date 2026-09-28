import { describe, expect, it } from "vitest";
import type { StatBlock } from "~/utils/api/types";
import {
    abilityRows,
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
