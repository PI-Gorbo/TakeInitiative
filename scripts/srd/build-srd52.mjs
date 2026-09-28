#!/usr/bin/env node
// Transcribes SRD 5.2's monsters into TakeInitiative's own JSON (roadmap step 20a).
//
//   pnpm srd:build                 # the pinned commit below
//   pnpm srd:build --sha <commit>  # a deliberate rebuild: bump PINNED_SHA and review the diff
//   pnpm srd:build --from <dir>    # read Creature.json, CreatureTrait.json, CreatureAction.json
//                                  # from a local folder instead of downloading them
//
// Run by hand only, never in CI or at build time. It reads the SRD 5.2 creature fixtures from
// Open5e's repository (github.com/open5e/open5e-api), which transcribe Wizards of the Coast's
// System Reference Document 5.2 (CC-BY-4.0). Only the SRD's own text and numbers are kept; no
// Open5e code, ids or artwork. The output is our schema, so the API never depends on theirs.
//
// Writes apps/TakeInitiative.Api/Reference/Srd52/{monsters,source}.json. There is no timestamp:
// a rebuild at the same sha is byte for byte the same.

import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const PINNED_SHA = "644ef5c4ebe1c996ee9ced0b42a6d8a2aad861bc";
const REPO = "open5e/open5e-api";
const FIXTURES = "data/v2/wizards-of-the-coast/srd-2024";
const MIN_CREATURES = 300;

const ATTRIBUTION =
    'This work includes material from the System Reference Document 5.2 ("SRD 5.2") by Wizards of ' +
    "the Coast LLC, available at https://www.dndbeyond.com/srd. The SRD 5.2 is licensed under the " +
    "Creative Commons Attribution 4.0 International License, available at " +
    "https://creativecommons.org/licenses/by/4.0/legalcode.";
const CHANGES =
    "Converted from Open5e's transcription into TakeInitiative's JSON format, and edited: the " +
    '"Hit:" labels of attack rolls restored, stray markup removed, usage limits moved into the ' +
    "names, and CR, XP and proficiency bonus written the SRD's way.";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const outDir = path.join(root, "apps/TakeInitiative.Api/Reference/Srd52");

// --- arguments --------------------------------------------------------------------------------

const args = process.argv.slice(2);
const option = (name) => {
    const i = args.indexOf(name);
    if (i < 0) return undefined;
    if (i + 1 >= args.length) fail(`${name} needs a value`);
    return args[i + 1];
};
const sha = option("--sha") ?? PINNED_SHA;
const from = option("--from");
if (!/^[0-9a-f]{40}$/.test(sha)) fail(`--sha must be a full 40-character commit sha, got "${sha}"`);

function fail(message) {
    console.error(`srd:build: ${message}`);
    process.exit(1);
}

// --- input ------------------------------------------------------------------------------------

async function load(file) {
    if (from) return JSON.parse(await readFile(path.join(from, file), "utf8"));
    const url = `https://raw.githubusercontent.com/${REPO}/${sha}/${FIXTURES}/${file}`;
    const response = await fetch(url);
    if (!response.ok) fail(`${url} answered ${response.status}`);
    return JSON.parse(await response.text());
}

const [creatures, traits, actions] = await Promise.all(
    ["Creature.json", "CreatureTrait.json", "CreatureAction.json"].map(load),
);
if (creatures.length < MIN_CREATURES) fail(`only ${creatures.length} creatures; expected at least ${MIN_CREATURES}`);

// --- the SRD's tables -------------------------------------------------------------------------

const XP = {
    "1/8": 25, "1/4": 50, "1/2": 100, 1: 200, 2: 450, 3: 700, 4: 1100, 5: 1800, 6: 2300, 7: 2900,
    8: 3900, 9: 5000, 10: 5900, 11: 7200, 12: 8400, 13: 10000, 14: 11500, 15: 13000, 16: 15000,
    17: 18000, 18: 20000, 19: 22000, 20: 25000, 21: 33000, 22: 41000, 23: 50000, 24: 62000,
    25: 75000, 26: 90000, 27: 105000, 28: 120000, 29: 135000, 30: 155000,
};

function formatCr(raw) {
    const value = Number(raw);
    const fractions = { 0.125: "1/8", 0.25: "1/4", 0.5: "1/2" };
    if (fractions[value]) return fractions[value];
    if (Number.isInteger(value) && value >= 0 && value <= 30) return String(value);
    fail(`unknown challenge rating "${raw}"`);
}

function proficiencyBonus(cr) {
    const value = cr.includes("/") ? 0 : Number(cr);
    return value <= 4 ? 2 : Math.min(9, 2 + Math.ceil((value - 4) / 4));
}

// SRD 5.2 gives CR 0 as "XP 0 or 10": 10 when the creature can deal damage.
function experience(cr, rows) {
    if (cr !== "0") return XP[cr];
    return rows.some((row) => /\d+ \(\d+d\d+[^)]*\) \w+ damage|\b\d+ \w+ damage/.test(row.text)) ? 10 : 0;
}

// --- text fixes -------------------------------------------------------------------------------

const fixes = { hit: 0, toHit: 0, feet: 0, tags: 0, hover: 0, recharge: 0 };

function fixText(text) {
    let out = text;
    // "+17 to hit, reach" is the 2014 form; 5.2 prints "+17, reach".
    out = out.replace(/(Attack Roll: [+-]\d+) to hit,/g, (_, head) => (fixes.toHit++, `${head},`));
    // "reach 5 feet." in an attack roll is "reach 5 ft." everywhere else.
    out = out.replace(/(Attack Roll: [^.]*?(?:reach|range) [\d/]+) feet\./g, (_, head) => (fixes.feet++, `${head} ft.`));
    // Open5e drops the "Hit:" label that follows an attack roll's reach or range sentence.
    if (!out.includes("Hit:")) {
        out = out.replace(
            /(Attack Roll: [+-]\d+(?: \([^)]*\))?, (?:reach|range) .*?ft\.) (?=\d|The target)/,
            (_, head) => (fixes.hit++, `${head} Hit: `),
        );
    }
    // 5eTools link residue, "<link target>|XPHB|<shown text>": keep the shown text.
    // "Sphere [Area of Effect]|XPHB|Sphere" is "Sphere", "Cover|XPHB|Total Cover" is "Total Cover".
    out = out.replace(/(\w+) \[Area of Effect\]\|XPHB\|\1/g, (_, word) => (fixes.tags++, word));
    out = out.replace(/\b(?:Cover|Short Rest)\|XPHB\|/g, () => (fixes.tags++, ""));
    out = out.replace(/ \[hover\]/g, () => (fixes.hover++, " (hover)"));
    return out;
}

// The only markup we let through: _italic_ runs, **bold** runs, blank-line paragraphs and "- "
// list items. Anything else is new, so stop and look at it rather than let the UI meet it.
function checkMarkup(where, text) {
    const bare = text.replace(/\*\*[^*\n]+\*\*/g, "").replace(/_[^_\n]+_/g, "");
    const bad = bare.match(/[*_[\]<>#`|{}]/);
    if (bad) fail(`${where}: unexpected markup "${bad[0]}" in: ${text.slice(0, 160)}`);
}

// --- usage limits -----------------------------------------------------------------------------

function fixName(name) {
    return name.replace(/Recharge (\d)-6/, (_, n) => (fixes.recharge++, `Recharge ${n}–6`));
}

function usage(action) {
    const { uses_type: type, uses_param: param, limited_to_form: form } = action;
    const parts = [];
    if (type === "PER_DAY") parts.push(`${param}/Day`);
    else if (type === "RECHARGE" || type === "RECHARGE_ON_ROLL") {
        if (param != null) parts.push(param === 6 ? "Recharge 6" : `Recharge ${param}–6`);
    } else if (type != null) fail(`unknown uses_type "${type}" on ${action.parent}/${action.name}`);
    if (form) parts.push(form);
    return parts;
}

// --- mapping ----------------------------------------------------------------------------------

const KINDS = { ACTION: "Action", BONUS_ACTION: "BonusAction", REACTION: "Reaction", LEGENDARY_ACTION: "LegendaryAction" };
const KIND_ORDER = Object.values(KINDS);
const SKILLS = [
    "acrobatics", "animal_handling", "arcana", "athletics", "deception", "history", "insight",
    "intimidation", "investigation", "medicine", "nature", "perception", "performance", "persuasion",
    "religion", "sleight_of_hand", "stealth", "survival",
];
const ABILITIES = { str: "strength", dex: "dexterity", con: "constitution", int: "intelligence", wis: "wisdom", cha: "charisma" };
const SENSES = [["blindsight_range", "Blindsight"], ["darkvision_range", "Darkvision"], ["tremorsense_range", "Tremorsense"], ["truesight_range", "Truesight"]];

const titleCase = (s) => s.replace(/\b[a-z]/g, (c) => c.toUpperCase());
const camel = (s) => s.replace(/_([a-z])/g, (_, c) => c.toUpperCase());
const list = (s) => (s ? s.split(", ").map(titleCase).join(", ") : "");

function required(fields, key, where) {
    const value = fields[key];
    if (value === null || value === undefined || value === "") fail(`${where} has no ${key}`);
    return value;
}

const traitsBy = Map.groupBy(traits.map((t) => t.fields), (t) => t.parent);
const actionsBy = Map.groupBy(actions.map((a) => a.fields), (a) => a.parent);

const monsters = creatures.map(({ pk, fields: f }) => {
    const id = pk.replace(/^srd-2024_/, "");
    if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(id)) fail(`unexpected id "${pk}"`);

    const hitDice = required(f, "hit_dice", id).replace(/\s+/g, "");
    if (!/^\d+d\d+([+-]\d+)?$/.test(hitDice)) fail(`${id}: unexpected hit dice "${f.hit_dice}"`);

    const cr = formatCr(required(f, "challenge_rating", id));

    const traitRows = (traitsBy.get(pk) ?? []).map((t) => {
        if (t.type != null) fail(`${id}: unexpected trait type "${t.type}"`);
        return { name: fixName(t.name), text: fixText(t.desc) };
    });

    const actionRows = (actionsBy.get(pk) ?? [])
        .map((a) => {
            const kind = KINDS[a.action_type];
            if (!kind) fail(`${id}: unknown action_type "${a.action_type}"`);
            if (a.legendary_action_cost != null && a.legendary_action_cost !== 1) {
                fail(`${id}/${a.name}: legendary action cost ${a.legendary_action_cost} is not handled`);
            }
            const name = fixName(a.name);
            const tags = name.includes("(") ? [] : usage(a);
            return {
                kind,
                order: a.order_in_statblock,
                name: tags.length ? `${name} (${tags.join("; ")})` : name,
                text: fixText(a.desc),
            };
        })
        .sort((a, b) => KIND_ORDER.indexOf(a.kind) - KIND_ORDER.indexOf(b.kind) || a.order - b.order)
        .map(({ kind, name, text }) => ({ kind, name, text }));

    for (const row of [...traitRows, ...actionRows]) checkMarkup(`${id}/${row.name}`, row.text);

    const senses = SENSES.filter(([key]) => f[key]).map(([key, label]) => `${label} ${f[key]} ft.`);
    const passive = required(f, "passive_perception", id);

    return {
        id,
        name: required(f, "name", id),
        category: f.category === "Monsters" ? "Monster" : "Animal",
        size: titleCase(required(f, "size", id)),
        type: titleCase(required(f, "type", id)),
        alignment: titleCase(required(f, "alignment", id)),
        ac: required(f, "armor_class", id),
        acNote: f.armor_detail || "",
        initiativeBonus: required(f, "initiative_bonus", id),
        hp: required(f, "hit_points", id),
        hitDice,
        speed: { walk: f.walk, fly: f.fly, swim: f.swim, climb: f.climb, burrow: f.burrow, hover: Boolean(f.hover) },
        abilities: Object.fromEntries(Object.entries(ABILITIES).map(([k, v]) => [k, required(f, `ability_score_${v}`, id)])),
        saves: Object.fromEntries(Object.entries(ABILITIES).map(([k, v]) => [k, required(f, `saving_throw_${v}`, id)])),
        skills: Object.fromEntries(SKILLS.filter((s) => f[`skill_bonus_${s}`] != null).map((s) => [camel(s), f[`skill_bonus_${s}`]])),
        vulnerabilities: list(f.damage_vulnerabilities_display),
        resistances: list(f.damage_resistances_display),
        immunities: list(f.damage_immunities_display),
        conditionImmunities: list(f.condition_immunities_display),
        senses: [senses.join(", "), `Passive Perception ${passive}`].filter(Boolean).join("; "),
        languages: f.languages_desc || "",
        cr,
        xp: experience(cr, actionRows),
        pb: proficiencyBonus(cr),
        traits: traitRows,
        actions: actionRows,
    };
});

// --- checks -----------------------------------------------------------------------------------

const seen = (key) => {
    const set = new Set();
    for (const m of monsters) {
        const value = key(m).toLowerCase();
        if (set.has(value)) fail(`duplicate ${value}`);
        set.add(value);
    }
};
seen((m) => m.id);
seen((m) => m.name);
for (const m of monsters) {
    if (!Number.isInteger(m.ac) || !Number.isInteger(m.hp) || !Number.isInteger(m.initiativeBonus)) fail(`${m.id}: AC, HP or initiative is not a number`);
}
const orphans = [...traitsBy.keys(), ...actionsBy.keys()].filter((parent) => !creatures.some((c) => c.pk === parent));
if (orphans.length) fail(`traits or actions for unknown creatures: ${orphans.join(", ")}`);

// --- output -----------------------------------------------------------------------------------

monsters.sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));

const source = {
    document: "SRD 5.2",
    license: "CC-BY-4.0",
    licenseUrl: "https://creativecommons.org/licenses/by/4.0/legalcode",
    sourceUrl: "https://www.dndbeyond.com/srd",
    sourceRepo: `https://github.com/${REPO}`,
    sha,
    count: monsters.length,
    attribution: ATTRIBUTION,
    changes: CHANGES,
};

await mkdir(outDir, { recursive: true });
await writeFile(path.join(outDir, "monsters.json"), `[\n${monsters.map((m) => JSON.stringify(m)).join(",\n")}\n]\n`);
await writeFile(path.join(outDir, "source.json"), `${JSON.stringify(source, null, 4)}\n`);

const attacks = monsters.flatMap((m) => m.actions).filter((a) => a.text.includes("Attack Roll:"));
const withoutHit = attacks.filter((a) => !a.text.includes("Hit:"));
console.log(`srd:build: ${monsters.length} monsters from ${REPO}@${sha.slice(0, 7)}`);
console.log(`  fixes: ${Object.entries(fixes).map(([k, v]) => `${k} ${v}`).join(", ")}`);
console.log(`  attack rolls: ${attacks.length}, without "Hit:" ${withoutHit.length}`);
for (const a of withoutHit) console.log(`    ${a.name}: ${a.text.slice(0, 100)}`);
