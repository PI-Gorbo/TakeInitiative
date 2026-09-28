// The stat-block card's pure rules (20c): SRD 5.2's header lines, the ability table,
// grouping actions by kind, the rules text as blocks of text runs, and the card's route.
// `Reference/StatBlockCard.vue` only draws these.
//
// The rules text is never markdown and never `v-html`. The build script (20a) lets through
// only `**bold**`, `_italic_`, blank-line paragraphs and `- ` list lines, and fails on
// anything else, so those four are all this reads. Everything becomes Vue text nodes, so a
// bad transcription can never inject markup.
import type { SearchReferenceHit, StatBlock, StatBlockAction, StatBlockSpeed } from "./api/types";

// ── Numbers ──────────────────────────────────────────────────────────────────

/** A signed number as the SRD prints it: "+2", "+0", "−1" (a real minus sign). */
export const signed = (n: number) => (n < 0 ? `−${Math.abs(n)}` : `+${n}`);

/** An ability score's modifier: 10 → "+0", 1 → "−5", 30 → "+10". */
export const modifier = (score: number) => signed(Math.floor((score - 10) / 2));

// ── Header lines ─────────────────────────────────────────────────────────────

/** "Small Fey, Chaotic Neutral". */
export const typeLine = (block: Pick<StatBlock, "size" | "type" | "alignment">) =>
    [`${block.size} ${block.type}`.trim(), block.alignment].filter(Boolean).join(", ");

/** 5.2's initiative: the bonus, then the passive score, 10 + the bonus: "+2 (12)". */
export const initiativeLine = (bonus: number) => `${signed(bonus)} (${10 + bonus})`;

/** "10 (3d6)". */
export const hpLine = (block: Pick<StatBlock, "hp" | "hitDice">) =>
    block.hitDice ? `${block.hp} (${block.hitDice})` : String(block.hp);

const OTHER_SPEEDS = [
    ["burrow", "Burrow"],
    ["climb", "Climb"],
    ["fly", "Fly"],
    ["swim", "Swim"],
] as const;

/** "30 ft., Climb 30 ft., Fly 60 ft. (hover)": walking first, then the rest A–Z. */
export function speedLine(speed: StatBlockSpeed): string {
    const parts: string[] = [];
    if (speed.walk != null) parts.push(`${speed.walk} ft.`);
    for (const [key, label] of OTHER_SPEEDS) {
        const value = speed[key];
        if (value == null) continue;
        parts.push(`${label} ${value} ft.${key === "fly" && speed.hover ? " (hover)" : ""}`);
    }
    return parts.join(", ");
}

/** "Perception +10, Stealth +6": the skills A–Z, `sleightOfHand` as "Sleight of Hand". */
export function skillsLine(skills: Record<string, number>): string {
    return Object.entries(skills)
        .map(([key, value]) => [skillName(key), value] as const)
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([name, value]) => `${name} ${signed(value)}`)
        .join(", ");
}

const SMALL_WORDS = new Set(["of", "and"]);
function skillName(key: string): string {
    return key
        .replace(/([a-z])([A-Z])/g, "$1 $2")
        .split(" ")
        .map((word, i) => {
            const lower = word.toLowerCase();
            return i > 0 && SMALL_WORDS.has(lower) ? lower : lower.charAt(0).toUpperCase() + lower.slice(1);
        })
        .join(" ");
}

/** "1/4 (XP 50; PB +2)". */
export const crLine = (block: Pick<StatBlock, "cr" | "xp" | "pb">) =>
    `${block.cr} (XP ${block.xp.toLocaleString("en-US")}; PB ${signed(block.pb)})`;

/**
 * The lines between the ability table and CR, each only when set. Languages is always
 * there, as "None" when empty, the way the SRD prints it.
 */
export function detailLines(block: StatBlock): { label: string; text: string }[] {
    const skills = skillsLine(block.skills ?? {});
    const immunities = [block.immunities, block.conditionImmunities].filter(Boolean).join("; ");
    return [
        { label: "Skills", text: skills },
        { label: "Vulnerabilities", text: block.vulnerabilities },
        { label: "Resistances", text: block.resistances },
        { label: "Immunities", text: immunities },
        { label: "Senses", text: block.senses },
        { label: "Languages", text: block.languages || "None" },
    ].filter((line) => !!line.text);
}

// ── The ability table ────────────────────────────────────────────────────────

export const ABILITIES = ["str", "dex", "con", "int", "wis", "cha"] as const;
export type Ability = (typeof ABILITIES)[number];

export type AbilityRow = { key: Ability; label: string; score: number; mod: string; save: string };

/** One row per ability, with 5.2's MOD and SAVE columns (`saves` is the save's bonus). */
export const abilityRows = (block: Pick<StatBlock, "abilities" | "saves">): AbilityRow[] =>
    ABILITIES.map((key) => ({
        key,
        label: key.toUpperCase(),
        score: block.abilities[key],
        mod: modifier(block.abilities[key]),
        save: signed(block.saves[key]),
    }));

// ── Traits and actions ───────────────────────────────────────────────────────

export type StatBlockRow = { name: string; text: string };
export type StatBlockSection = { key: string; title: string; rows: StatBlockRow[] };

const ACTION_SECTIONS: { kind: StatBlockAction["kind"]; title: string }[] = [
    { kind: "Action", title: "Actions" },
    { kind: "BonusAction", title: "Bonus Actions" },
    { kind: "Reaction", title: "Reactions" },
    { kind: "LegendaryAction", title: "Legendary Actions" },
];

/** Traits, Actions, Bonus Actions, Reactions and Legendary Actions, each only with rows, in the data's order. */
export function statBlockSections(block: Pick<StatBlock, "traits" | "actions">): StatBlockSection[] {
    const sections: StatBlockSection[] = [{ key: "Trait", title: "Traits", rows: block.traits ?? [] }];
    for (const { kind, title } of ACTION_SECTIONS) {
        sections.push({ key: kind, title, rows: (block.actions ?? []).filter((a) => a.kind === kind) });
    }
    return sections.filter((s) => s.rows.length > 0);
}

// ── Rules text ───────────────────────────────────────────────────────────────

export type TextRun = { text: string; bold?: true; italic?: true };
export type TextBlock = { type: "paragraph"; runs: TextRun[] } | { type: "list"; items: TextRun[][] };

const INLINE = /\*\*([^*\n]+?)\*\*|_([^_\n]+?)_/g;

/** One line as runs: `**…**` bold, `_…_` italic, everything else plain. */
export function textRuns(line: string): TextRun[] {
    const runs: TextRun[] = [];
    let at = 0;
    for (const match of line.matchAll(INLINE)) {
        const start = match.index ?? 0;
        if (start > at) runs.push({ text: line.slice(at, start) });
        if (match[1] !== undefined) runs.push({ text: match[1], bold: true });
        else runs.push({ text: match[2]!, italic: true });
        at = start + match[0].length;
    }
    if (at < line.length) runs.push({ text: line.slice(at) });
    return runs;
}

/**
 * A trait's or action's text as blocks: blank lines split paragraphs, and runs of `- `
 * lines are a list. Other lines next to each other join into one paragraph.
 */
export function textBlocks(text: string): TextBlock[] {
    const blocks: TextBlock[] = [];
    for (const chunk of text.split(/\n\s*\n/)) {
        let paragraph: string[] = [];
        let list: TextRun[][] | null = null;
        const flushParagraph = () => {
            if (paragraph.length) blocks.push({ type: "paragraph", runs: textRuns(paragraph.join(" ")) });
            paragraph = [];
        };
        const flushList = () => {
            if (list?.length) blocks.push({ type: "list", items: list });
            list = null;
        };
        for (const raw of chunk.split("\n")) {
            const line = raw.trim();
            if (!line) continue;
            if (line.startsWith("- ")) {
                flushParagraph();
                (list ??= []).push(textRuns(line.slice(2).trim()));
            } else {
                flushList();
                paragraph.push(line);
            }
        }
        flushParagraph();
        flushList();
    }
    return blocks;
}

// ── Routes and rows ──────────────────────────────────────────────────────────

/** The stat-block card's page in a campaign. */
export const referencePath = (campaignId: string, provider: string, itemId: string) =>
    `/app/campaigns/${encodeURIComponent(campaignId)}/reference/${encodeURIComponent(provider)}/${encodeURIComponent(itemId)}`;

/** A ⌘K reference row's line: "Monster · CR 1/4 · SRD 5.2". */
export function referenceHitLine(hit: Pick<SearchReferenceHit, "category" | "detail" | "providerLabel">): string {
    // The API's detail is "CR 1/4 · Small Fey"; the row keeps only the CR.
    const cr = hit.detail.split(" · ").find((part) => part.startsWith("CR "));
    return [hit.category, cr, hit.providerLabel].filter(Boolean).join(" · ");
}

// ── Attribution ──────────────────────────────────────────────────────────────

export type AttributionPart = { text: string; href?: string };

const URL_IN_TEXT = /https:\/\/[^\s"<>]+?(?=[.,;:)]?(?:\s|$))/g;

/**
 * The attribution statement as text and links: each `https://` URL in it becomes a link,
 * without the full stop that ends its sentence. The words are the API's, untouched.
 */
export function attributionParts(text: string): AttributionPart[] {
    const parts: AttributionPart[] = [];
    let at = 0;
    for (const match of text.matchAll(URL_IN_TEXT)) {
        const start = match.index ?? 0;
        if (start > at) parts.push({ text: text.slice(at, start) });
        parts.push({ text: match[0], href: match[0] });
        at = start + match[0].length;
    }
    if (at < text.length) parts.push({ text: text.slice(at) });
    return parts;
}
