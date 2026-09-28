#!/usr/bin/env node
// Builds the 5eTools search index (roadmap step 21a).
//
//   pnpm 5etools:build --from <dir>        # a 5etools source checkout, or its data/ folder
//   pnpm 5etools:build --from <dir> --no-stats
//
// Options:
//   --from <dir>          required; a local copy of the 5etools source data the deployer supplies
//   --out <file>          default .data/5etools/index.json (git-ignored)
//   --no-stats            write stats: null on every row
//   --base-url <url>      default https://5e.tools
//   --min-monsters <n>    default 1000; a lower count means a wrong folder (the tests pass 1)
//
// Run by hand only, never in CI or at build time. It NEVER downloads anything: getting a copy of
// the 5etools data is the deployer's own choice. The data is copyrighted and not licensed for
// reuse, so the index holds only identifiers and a few numbers, built field by field from an
// allowlist (ITEM_KEYS, STATS_KEYS below): name, category, source book, page, a short label built
// from enums, a deep link, and for monsters AC, HP dice and initiative bonus. No rules text,
// descriptions, stat blocks, images or any other 5eTools field is ever written. Any change to the
// allowlist is a decision for the user, not an implementation detail (step 21's Notes).
//
// There is no timestamp: a rebuild from the same 5eTools version is byte for byte the same.

import { existsSync } from "node:fs";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

export const FORMAT = 1;
export const DEFAULT_BASE_URL = "https://5e.tools";
const DEFAULT_MIN_MONSTERS = 1000;

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
export const DEFAULT_OUT = path.join(root, ".data/5etools/index.json");

// --- the allowlist ----------------------------------------------------------------------------

export const TOP_KEYS = ["format", "fiveEToolsVersion", "counts", "sources", "items"];
export const ITEM_KEYS = ["id", "name", "category", "source", "page", "url", "label", "stats"];
export const STATS_KEYS = ["ac", "hp", "initiativeBonus"];
const CATEGORIES = ["Monster", "Spell", "Item"];
const PAGES = { Monster: "bestiary", Spell: "spells", Item: "items" };

export class BuildError extends Error {}
const fail = (message) => {
    throw new BuildError(message);
};

// --- enums the script maps itself -------------------------------------------------------------

const SIZES = { F: "Fine", D: "Diminutive", T: "Tiny", S: "Small", M: "Medium", L: "Large", H: "Huge", G: "Gargantuan", C: "Colossal", V: "Varies" };
const TYPES = [
    "aberration", "beast", "celestial", "construct", "dragon", "elemental", "fey", "fiend", "giant",
    "humanoid", "monstrosity", "ooze", "plant", "undead",
];
const SCHOOLS = { A: "Abjuration", C: "Conjuration", D: "Divination", E: "Enchantment", V: "Evocation", I: "Illusion", N: "Necromancy", T: "Transmutation", P: "Psionic" };
const RARITIES = {
    common: "Common", uncommon: "Uncommon", rare: "Rare", "very rare": "Very Rare", legendary: "Legendary",
    artifact: "Artifact", varies: "Varies",
};
const ITEM_TYPES = {
    A: "Ammunition", AF: "Ammunition", AIR: "Vehicle", AT: "Artisan's Tools", EXP: "Explosive",
    FD: "Food and Drink", G: "Adventuring Gear", GS: "Gaming Set", HA: "Heavy Armor", INS: "Instrument",
    LA: "Light Armor", M: "Melee Weapon", MA: "Medium Armor", MNT: "Mount", P: "Potion", R: "Ranged Weapon",
    RD: "Rod", RG: "Ring", S: "Shield", SC: "Scroll", SCF: "Spellcasting Focus", SHP: "Vehicle",
    SPC: "Vehicle", ST: "Staff", T: "Tools", TAH: "Tack and Harness", TG: "Trade Good", VEH: "Vehicle",
    WD: "Wand", $: "Treasure", $A: "Treasure", $C: "Treasure", $G: "Treasure",
};
const CRS = new Set(["0", "1/8", "1/4", "1/2", ...Array.from({ length: 30 }, (_, i) => String(i + 1))]);

const titleCase = (s) => s.replace(/\b[a-z]/g, (c) => c.toUpperCase());

// --- ids and links ----------------------------------------------------------------------------

export function slug(name) {
    return name
        .normalize("NFKD")
        .replace(/[̀-ͯ]/g, "")
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "");
}

// 5etools' UrlUtil.encodeForHash: each part lower-cased and URI-encoded, joined with "_".
export function encodeHash(...parts) {
    return parts.map((part) => encodeURIComponent(String(part).toLowerCase())).join("_");
}

export function makeId(category, name, source) {
    const id = `${category.toLowerCase()}_${slug(name)}_${source.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;
    if (!/^[a-z]+_[a-z0-9-]+_[a-z0-9-]+$/.test(id)) fail(`cannot make an id for ${category} "${name}" (${source})`);
    return id;
}

// --- monsters ---------------------------------------------------------------------------------

export function crOf(raw) {
    const value = raw && typeof raw === "object" ? raw.cr : raw;
    return typeof value === "string" && CRS.has(value) ? value : null;
}

export function proficiencyBonus(cr) {
    if (cr.includes("/")) return 2;
    return Math.max(2, 2 + Math.ceil((Number(cr) - 4) / 4));
}

export function acOf(raw) {
    if (!Array.isArray(raw) || raw.length === 0) return null;
    const first = raw[0];
    if (Number.isInteger(first)) return first;
    if (first && typeof first === "object" && Number.isInteger(first.ac)) return first.ac;
    return null;
}

export function hpDiceOf(raw) {
    if (!raw || typeof raw !== "object" || typeof raw.formula !== "string") return null;
    const dice = raw.formula.replace(/\s+/g, "");
    return /^\d+d\d+([+-]\d+)?$/.test(dice) ? dice : null;
}

export function initiativeOf(monster) {
    const init = monster.initiative;
    if (init && typeof init === "object" && Number.isInteger(init.initiative)) return init.initiative;
    if (!Number.isInteger(monster.dex)) return null;
    const dexMod = Math.floor((monster.dex - 10) / 2);
    const proficiency = init && typeof init === "object" && Number.isInteger(init.proficiency) ? init.proficiency : 0;
    if (proficiency === 0) return dexMod;
    const cr = crOf(monster.cr);
    return cr === null ? null : dexMod + proficiency * proficiencyBonus(cr);
}

function sizeLabel(size, where) {
    const letters = Array.isArray(size) ? size : size == null ? [] : [size];
    return letters
        .map((letter) => SIZES[letter] ?? fail(`${where}: unknown size "${letter}"`))
        .join(" or ");
}

function typeLabel(type) {
    // A string, or { type, tags, swarmSize }, or { type: { choose: [...] } }. Tags and any other
    // free text are dropped; only the enum is mapped.
    const value = type && typeof type === "object" ? type.type : type;
    const words = value && typeof value === "object" && Array.isArray(value.choose) ? value.choose : [value];
    const known = words.filter((w) => typeof w === "string" && TYPES.includes(w.toLowerCase()));
    return known.map((w) => titleCase(w.toLowerCase())).join(" or ");
}

function monsterLabel(m, where) {
    const cr = crOf(m.cr);
    const kind = [sizeLabel(m.size, where), typeLabel(m.type)].filter(Boolean).join(" ");
    return [cr === null ? "" : `CR ${cr}`, kind].filter(Boolean).join(" · ") || null;
}

// The fields a copy may take from its base. Only what the index needs; `_mod` edits text the index
// never stores, and `_templates` are counted and ignored.
const COPY_FIELDS = ["size", "type", "ac", "hp", "cr", "dex", "initiative"];

// --- spells and items -------------------------------------------------------------------------

function spellLabel(s, where) {
    const school = SCHOOLS[s.school] ?? fail(`${where}: unknown school "${s.school}"`);
    if (!Number.isInteger(s.level)) fail(`${where}: level is not a number`);
    return s.level === 0 ? `${school} Cantrip` : `Level ${s.level} ${school}`;
}

function itemLabel(it) {
    const rarity = typeof it.rarity === "string" ? RARITIES[it.rarity.toLowerCase()] ?? "" : "";
    const code = typeof it.type === "string" ? it.type.split("|")[0] : "";
    const type = it.wondrous === true ? "Wondrous Item" : ITEM_TYPES[code] ?? "";
    return [rarity, type].filter(Boolean).join(" ") || null;
}

// --- input ------------------------------------------------------------------------------------

export function findDataDir(from) {
    const dir = path.resolve(from);
    if (existsSync(path.join(dir, "data", "bestiary"))) return { data: path.join(dir, "data"), checkout: dir };
    if (existsSync(path.join(dir, "bestiary"))) return { data: dir, checkout: path.dirname(dir) };
    fail(`no 5etools data/ folder in ${dir} (expected data/bestiary/ or bestiary/)`);
}

async function readJson(file) {
    try {
        return JSON.parse(await readFile(file, "utf8"));
    } catch (error) {
        fail(`cannot read ${file}: ${error.message}`);
    }
}

async function readOptionalJson(file) {
    return existsSync(file) ? readJson(file) : null;
}

// index.json maps a source to a file; only files with the right prefix and no "fluff" are read.
async function readIndexed(dir, prefix, key) {
    const index = await readJson(path.join(dir, "index.json"));
    const files = [...new Set(Object.values(index))].filter(
        (f) => typeof f === "string" && f.startsWith(prefix) && !f.includes("fluff") && f.endsWith(".json"),
    );
    files.sort();
    const rows = [];
    for (const file of files) {
        const json = await readJson(path.join(dir, file));
        for (const row of json[key] ?? []) rows.push(row);
    }
    return { rows, files: files.length };
}

const isUa = (source) => typeof source === "string" && /^UA/i.test(source);
const keyOf = (name, source) => `${String(name).toLowerCase()}|${String(source).toLowerCase()}`;

// --- build ------------------------------------------------------------------------------------

export async function buildIndex({ from, noStats = false, baseUrl = DEFAULT_BASE_URL, minMonsters = DEFAULT_MIN_MONSTERS }) {
    const { data, checkout } = findDataDir(from);
    const base = baseUrl.replace(/\/+$/, "");
    const report = {
        read: { monster: 0, spell: 0, item: 0 },
        skipped: { ua: 0, srd52: 0, missingBase: [], broken: [], noName: 0 },
        templates: 0,
        withoutStats: 0,
    };

    const pkg = await readOptionalJson(path.join(checkout, "package.json"));
    const version = pkg && typeof pkg.version === "string" && /^[\w.+-]+$/.test(pkg.version) ? pkg.version : null;

    const titles = new Map();
    for (const [file, key] of [["books.json", "book"], ["adventures.json", "adventure"]]) {
        const json = await readOptionalJson(path.join(data, file));
        for (const b of json?.[key] ?? []) {
            const abbr = b.source ?? b.id;
            if (typeof abbr === "string" && typeof b.name === "string" && !titles.has(abbr)) titles.set(abbr, b.name);
        }
    }

    const items = [];
    const push = (category, raw, label, stats) => {
        items.push(pick(category, raw, label, stats, base));
    };
    const usable = (raw) => {
        if (typeof raw.name !== "string" || !raw.name || typeof raw.source !== "string" || !raw.source) {
            report.skipped.noName++;
            return false;
        }
        if (isUa(raw.source)) {
            report.skipped.ua++;
            return false;
        }
        return true;
    };

    // Monsters.
    const bestiary = await readIndexed(path.join(data, "bestiary"), "bestiary-", "monster");
    const byKey = new Map(bestiary.rows.filter((m) => m?.name && m?.source).map((m) => [keyOf(m.name, m.source), m]));
    const resolve = (m, seen = new Set()) => {
        if (!m._copy) return m;
        const k = keyOf(m._copy.name, m._copy.source);
        if (seen.has(k)) return null;
        seen.add(k);
        const found = byKey.get(k);
        const parent = found && resolve(found, seen);
        if (!parent) return null;
        const merged = {};
        for (const field of COPY_FIELDS) if (parent[field] !== undefined) merged[field] = parent[field];
        for (const field of COPY_FIELDS) if (m[field] !== undefined) merged[field] = m[field];
        return merged;
    };
    for (const raw of bestiary.rows) {
        report.read.monster++;
        if (!usable(raw)) continue;
        if (raw.srd52) {
            report.skipped.srd52++;
            continue;
        }
        const where = `monster "${raw.name}" (${raw.source})`;
        if (!raw.hp && !raw._copy) {
            report.skipped.broken.push(`${raw.name} (${raw.source})`);
            continue;
        }
        if (raw._copy?._templates) report.templates++;
        const m = resolve(raw);
        if (!m) {
            report.skipped.missingBase.push(`${raw.name} (${raw.source}) copies ${raw._copy.name} (${raw._copy.source})`);
            continue;
        }
        let stats = null;
        if (!noStats) {
            const ac = acOf(m.ac);
            const hp = hpDiceOf(m.hp);
            const initiativeBonus = initiativeOf(m);
            if (ac !== null && hp !== null && initiativeBonus !== null) stats = { ac, hp, initiativeBonus };
            else report.withoutStats++;
        }
        push("Monster", raw, monsterLabel(m, where), stats);
    }

    // Spells.
    const spells = await readIndexed(path.join(data, "spells"), "spells-", "spell");
    for (const raw of spells.rows) {
        report.read.spell++;
        if (!usable(raw)) continue;
        push("Spell", raw, spellLabel(raw, `spell "${raw.name}" (${raw.source})`), null);
    }

    // Items: magic and mundane items, then base items (weapons, armour, gear).
    const magic = (await readOptionalJson(path.join(data, "items.json")))?.item ?? [];
    const bases = (await readOptionalJson(path.join(data, "items-base.json")))?.baseitem ?? [];
    const itemsByKey = new Map([...magic, ...bases].filter((i) => i?.name && i?.source).map((i) => [keyOf(i.name, i.source), i]));
    for (const raw of [...magic, ...bases]) {
        report.read.item++;
        if (!usable(raw)) continue;
        const own = raw._copy ? { ...itemsByKey.get(keyOf(raw._copy.name, raw._copy.source)), ...raw } : raw;
        push("Item", raw, itemLabel(own), null);
    }

    const monsterCount = items.filter((i) => i.category === "Monster").length;
    if (monsterCount < minMonsters) fail(`only ${monsterCount} monsters; expected at least ${minMonsters} (is --from the right folder?)`);

    const order = (a, b) => (a < b ? -1 : a > b ? 1 : 0);
    items.sort(
        (a, b) =>
            CATEGORIES.indexOf(a.category) - CATEGORIES.indexOf(b.category) || order(a.name, b.name) || order(a.source, b.source),
    );

    const ids = new Set();
    for (const item of items) {
        if (ids.has(item.id)) fail(`duplicate id ${item.id}`);
        ids.add(item.id);
        check(item);
    }

    const usedSources = [...new Set(items.map((i) => i.source))].sort(order);
    const index = {
        format: FORMAT,
        fiveEToolsVersion: version,
        counts: {
            monster: monsterCount,
            spell: items.filter((i) => i.category === "Spell").length,
            item: items.filter((i) => i.category === "Item").length,
        },
        sources: Object.fromEntries(usedSources.map((s) => [s, titles.get(s) ?? s])),
        items,
    };
    if (JSON.stringify(Object.keys(index)) !== JSON.stringify(TOP_KEYS)) fail("the index has a key outside the allowlist");
    return { index, report };
}

// Builds one row from named fields. Never spread or copy a 5eTools object here.
function pick(category, raw, label, stats, base) {
    if (!CATEGORIES.includes(category)) fail(`unknown category ${category}`);
    return {
        id: makeId(category, raw.name, raw.source),
        name: raw.name,
        category,
        source: raw.source,
        page: Number.isInteger(raw.page) ? raw.page : null,
        url: `${base}/${PAGES[category]}.html#${encodeHash(raw.name, raw.source)}`,
        label,
        stats: stats && { ac: stats.ac, hp: stats.hp, initiativeBonus: stats.initiativeBonus },
    };
}

function check(item) {
    const keys = Object.keys(item);
    if (JSON.stringify(keys) !== JSON.stringify(ITEM_KEYS)) fail(`${item.id}: keys ${keys.join(", ")} are not the allowlist`);
    if (typeof item.name !== "string" || typeof item.source !== "string") fail(`${item.id}: name or source is not text`);
    if (item.label !== null && typeof item.label !== "string") fail(`${item.id}: label is not text`);
    if (item.stats !== null) {
        if (item.category !== "Monster") fail(`${item.id}: only monsters have stats`);
        if (JSON.stringify(Object.keys(item.stats)) !== JSON.stringify(STATS_KEYS)) fail(`${item.id}: stats keys are not the allowlist`);
        if (!Number.isInteger(item.stats.ac) || !Number.isInteger(item.stats.initiativeBonus)) fail(`${item.id}: AC or initiative is not a number`);
        if (!/^\d+d\d+([+-]\d+)?$/.test(item.stats.hp)) fail(`${item.id}: HP "${item.stats.hp}" is not NdM[+-K]`);
    }
}

export function serialize(index) {
    const { items, ...head } = index;
    const top = JSON.stringify(head).slice(0, -1);
    return `${top},"items":[\n${items.map((i) => JSON.stringify(i)).join(",\n")}\n]}\n`;
}

// --- command line -----------------------------------------------------------------------------

const displayPath = (file) => {
    const rel = path.relative(process.cwd(), file);
    return rel && !rel.startsWith("..") ? rel : file;
};

const USAGE = `usage: pnpm 5etools:build --from <5etools source checkout or its data/ folder> [--no-stats] [--out <file>] [--base-url <url>]

The script never downloads anything. Get a local copy of the 5etools source data yourself (for
example a checkout of the 5etools source repository) and pass its path with --from.`;

export async function main(argv) {
    const option = (name) => {
        const i = argv.indexOf(name);
        if (i < 0) return undefined;
        if (i + 1 >= argv.length || argv[i + 1].startsWith("--")) fail(`${name} needs a value`);
        return argv[i + 1];
    };
    const from = option("--from");
    if (!from) {
        console.error(USAGE);
        return 1;
    }
    const minRaw = option("--min-monsters");
    const minMonsters = minRaw === undefined ? DEFAULT_MIN_MONSTERS : Number(minRaw);
    if (!Number.isInteger(minMonsters) || minMonsters < 0) fail(`--min-monsters must be a whole number`);
    const out = path.resolve(option("--out") ?? DEFAULT_OUT);
    const { index, report } = await buildIndex({
        from,
        noStats: argv.includes("--no-stats"),
        baseUrl: option("--base-url") ?? DEFAULT_BASE_URL,
        minMonsters,
    });
    await mkdir(path.dirname(out), { recursive: true });
    await writeFile(out, serialize(index));

    const s = report.skipped;
    console.log(`5etools:build: ${displayPath(out)} from 5eTools ${index.fiveEToolsVersion ?? "(unknown version)"}`);
    console.log(`  read: ${report.read.monster} monsters, ${report.read.spell} spells, ${report.read.item} items`);
    console.log(`  wrote: ${index.counts.monster} monsters, ${index.counts.spell} spells, ${index.counts.item} items`);
    console.log(`  skipped: ${s.ua} UA, ${s.srd52} SRD 5.2 monsters, ${s.noName} without a name or source`);
    console.log(`  monsters: ${report.templates} copies with _templates (ignored), ${report.withoutStats} without stats${argv.includes("--no-stats") ? " (--no-stats)" : ""}`);
    for (const line of s.missingBase) console.log(`  skipped, base missing: ${line}`);
    for (const line of s.broken) console.log(`  skipped, no hp and no _copy: ${line}`);
    return 0;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
    try {
        process.exitCode = await main(process.argv.slice(2));
    } catch (error) {
        if (!(error instanceof BuildError)) throw error;
        console.error(`5etools:build: ${error.message}`);
        process.exitCode = 1;
    }
}
