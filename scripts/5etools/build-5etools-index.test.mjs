// Tests for the 5eTools index script (roadmap step 21a). Run with `pnpm 5etools:test`.
// The fixture is invented data shaped like 5eTools' data/ folder, under the made-up source TST.
// Never add a real 5eTools file here, not even an excerpt.

import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { after, describe, test } from "node:test";
import { fileURLToPath } from "node:url";

import {
    acOf, buildIndex, crOf, encodeHash, hpDiceOf, initiativeOf, ITEM_KEYS, makeId, serialize, STATS_KEYS, TOP_KEYS,
} from "./build-5etools-index.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const script = path.join(here, "build-5etools-index.mjs");
const fixture = path.join(here, "fixture");
const tmp = mkdtempSync(path.join(tmpdir(), "5etools-index-"));
after(() => rmSync(tmp, { recursive: true, force: true }));

const run = (...args) => spawnSync(process.execPath, [script, ...args], { encoding: "utf8" });
const build = (options = {}) => buildIndex({ from: fixture, minMonsters: 1, ...options });
const row = (index, id) => {
    const found = index.items.find((i) => i.id === id);
    assert.ok(found, `no row ${id}`);
    return found;
};

describe("the allowlist", () => {
    test("a fixture monster has exactly the allowed keys", async () => {
        const { index } = await build();
        const gremlin = row(index, "monster_test-gremlin_tst");
        assert.deepEqual(Object.keys(gremlin), ["id", "name", "category", "source", "page", "url", "label", "stats"]);
        assert.deepEqual(Object.keys(gremlin.stats), ["ac", "hp", "initiativeBonus"]);
        assert.deepEqual(gremlin, {
            id: "monster_test-gremlin_tst",
            name: "Test Gremlin",
            category: "Monster",
            source: "TST",
            page: 12,
            url: "https://5e.tools/bestiary.html#test%20gremlin_tst",
            label: "CR 1/2 · Small Fey",
            stats: { ac: 15, hp: "3d6+3", initiativeBonus: 2 },
        });
    });

    test("every row, and the file, has only allowed keys", async () => {
        const { index } = await build();
        assert.deepEqual(Object.keys(index), TOP_KEYS);
        for (const item of index.items) {
            assert.deepEqual(Object.keys(item), ITEM_KEYS, item.id);
            if (item.stats) assert.deepEqual(Object.keys(item.stats), STATS_KEYS, item.id);
        }
    });

    test("no rules text, fluff, tag or token reaches the output", async () => {
        const text = serialize((await build()).index);
        for (const word of ["zorblatt", "Traitword", "Actionword", "Entryword", "Fluffword", "Modword", "tagword", "kettlewhistle", "quibbleflux", "token", "Sylvan", "darkvision", "{@"]) {
            assert.ok(!text.toLowerCase().includes(word.toLowerCase()), `"${word}" leaked into the index`);
        }
    });
});

describe("monsters", () => {
    test("a _copy with _mod takes the base's fields under its own", async () => {
        const chief = row((await build()).index, "monster_test-gremlin-chief_tst");
        assert.equal(chief.label, "CR 2 · Small Fey");
        assert.deepEqual(chief.stats, { ac: 15, hp: "5d6+10", initiativeBonus: 4 });
    });

    test("a _copy without _mod, and one across files with _templates", async () => {
        const { index, report } = await build();
        assert.deepEqual(row(index, "monster_test-gremlin-understudy_tst").stats, { ac: 15, hp: "3d6+3", initiativeBonus: 2 });
        const zombie = row(index, "monster_test-gremlin-zombie_tsta");
        assert.equal(zombie.label, "CR 1/2 · Small Undead");
        assert.equal(report.templates, 1);
    });

    test("skips a copy with a missing base, a broken row, SRD 5.2 duplicates and UA", async () => {
        const { index, report } = await build();
        const names = index.items.map((i) => i.name);
        for (const name of ["Test Orphan Copy", "Test Hollow Sentinel", "Test Srd Imp", "Test Draft Horror", "Test Fluff Phantom"]) {
            assert.ok(!names.includes(name), name);
        }
        assert.equal(report.skipped.missingBase.length, 1);
        assert.equal(report.skipped.broken.length, 1);
        assert.equal(report.skipped.srd52, 1);
        assert.equal(report.skipped.ua, 1);
        assert.deepEqual(index.counts, { monster: 8, spell: 3, item: 3 });
    });

    test("special HP, special AC and an unknown CR with proficiency give no stats", async () => {
        const { index } = await build();
        assert.equal(row(index, "monster_test-swarm-of-motes_tst").stats, null);
        assert.equal(row(index, "monster_test-mossback_tst").stats, null);
        const finch = row(index, "monster_test-clockwork-finch_tst");
        assert.equal(finch.stats, null);
        assert.equal(finch.label, "Tiny Construct");
    });

    test("every AC shape", () => {
        assert.equal(acOf([15]), 15);
        assert.equal(acOf([{ ac: 17, from: ["x"] }, { ac: 19, condition: "y" }]), 17);
        assert.equal(acOf([{ special: "12 + PB" }]), null);
        assert.equal(acOf(undefined), null);
    });

    test("every CR shape", () => {
        assert.equal(crOf("1/4"), "1/4");
        assert.equal(crOf({ cr: "5", lair: "6", coven: "7" }), "5");
        assert.equal(crOf("Unknown"), null);
        assert.equal(crOf(undefined), null);
    });

    test("HP dice are normalised and checked", () => {
        assert.equal(hpDiceOf({ average: 85, formula: "10d10 + 30" }), "10d10+30");
        assert.equal(hpDiceOf({ formula: "1d4 - 1" }), "1d4-1");
        assert.equal(hpDiceOf({ special: "half" }), null);
        assert.equal(hpDiceOf({ formula: "2d8 + 3 + 1" }), null);
    });

    test("initiative, explicit, from DEX, and with proficiency", () => {
        assert.equal(initiativeOf({ dex: 8, initiative: { initiative: 7 }, cr: "5" }), 7);
        assert.equal(initiativeOf({ dex: 9, cr: "1" }), -1);
        assert.equal(initiativeOf({ dex: 14, initiative: { proficiency: 1 }, cr: "2" }), 4);
        assert.equal(initiativeOf({ dex: 14, initiative: { proficiency: 2 }, cr: { cr: "17" } }), 14);
        assert.equal(initiativeOf({ dex: 14, initiative: { proficiency: 1 }, cr: "Unknown" }), null);
        assert.equal(initiativeOf({ cr: "1" }), null);
    });

    test("labels drop free-text type tags", async () => {
        const warden = row((await build()).index, "monster_warden-s-stone-awakened_tst");
        assert.equal(warden.label, "CR 5 · Large or Huge Construct");
        assert.deepEqual(warden.stats, { ac: 17, hp: "10d10+30", initiativeBonus: 7 });
    });
});

describe("spells, items and sources", () => {
    test("labels come from enums", async () => {
        const { index } = await build();
        assert.equal(row(index, "spell_test-sparkburst_tst").label, "Level 3 Evocation");
        assert.equal(row(index, "spell_test-glimmer_tst").label, "Illusion Cantrip");
        assert.equal(row(index, "item_test-satchel-of-plenty_tst").label, "Uncommon Wondrous Item");
        assert.equal(row(index, "item_test-blade-of-echoes_tst").label, "Rare Melee Weapon");
        assert.equal(row(index, "item_test-pike_tst").label, "Melee Weapon");
        assert.equal(row(index, "spell_test-sparkburst_tst").url, "https://5e.tools/spells.html#test%20sparkburst_tst");
        assert.equal(row(index, "item_test-pike_tst").url, "https://5e.tools/items.html#test%20pike_tst");
    });

    test("SRD-flagged spells are kept, and no row but a monster's has stats", async () => {
        const { index } = await build();
        row(index, "spell_test-mending-hum_tst");
        assert.ok(index.items.filter((i) => i.category !== "Monster").every((i) => i.stats === null));
    });

    test("sources map to book and adventure titles", async () => {
        const { index } = await build();
        assert.deepEqual(index.sources, { TST: "Test Book of Beasts", TSTA: "Test Adventure in the Lint Caves" });
        assert.equal(index.fiveEToolsVersion, "0.0.0-test");
        assert.equal(index.format, 1);
    });

    test("sorted by category, then name, then source", async () => {
        const { index } = await build();
        const categories = index.items.map((i) => i.category);
        assert.deepEqual([...new Set(categories)], ["Monster", "Spell", "Item"]);
        const monsters = index.items.filter((i) => i.category === "Monster").map((i) => i.name);
        assert.deepEqual(monsters, [...monsters].sort());
    });
});

describe("ids and links", () => {
    test("the link encoder: spaces, apostrophes, commas and parentheses", () => {
        assert.equal(encodeHash("Goblin Boss", "XMM"), "goblin%20boss_xmm");
        assert.equal(encodeHash("Warden's Stone (Awakened)", "TST"), "warden's%20stone%20(awakened)_tst");
        assert.equal(encodeHash("Test Gremlin, Understudy", "TST"), "test%20gremlin%2C%20understudy_tst");
        assert.equal(encodeHash("Bag of Holding", "XDMG"), "bag%20of%20holding_xdmg");
    });

    test("ids are slugs", () => {
        assert.equal(makeId("Monster", "Goblin Boss", "XMM"), "monster_goblin-boss_xmm");
        assert.equal(makeId("Monster", "Warden's Stone (Awakened)", "TST"), "monster_warden-s-stone-awakened_tst");
        assert.equal(makeId("Item", "Bag of Holding", "XDMG"), "item_bag-of-holding_xdmg");
    });

    test("--base-url overrides the host", async () => {
        const { index } = await build({ baseUrl: "https://example.test/" });
        assert.equal(row(index, "monster_test-gremlin_tst").url, "https://example.test/bestiary.html#test%20gremlin_tst");
    });
});

describe("the command line", () => {
    test("writes the index, and a rebuild is byte for byte the same", () => {
        const out = path.join(tmp, "a", "index.json");
        const first = run("--from", fixture, "--min-monsters", "1", "--out", out);
        assert.equal(first.status, 0, first.stderr);
        const bytes = readFileSync(out);
        const second = run("--from", path.join(fixture, "data"), "--min-monsters", "1", "--out", out);
        assert.equal(second.status, 0, second.stderr);
        assert.ok(bytes.equals(readFileSync(out)));
        const index = JSON.parse(bytes.toString("utf8"));
        assert.equal(index.items.length, 14);
        assert.equal(bytes.toString("utf8").split("\n").length, 17, "one row per line");
    });

    test("--no-stats writes stats: null everywhere", () => {
        const out = path.join(tmp, "b", "index.json");
        const result = run("--from", fixture, "--min-monsters", "1", "--no-stats", "--out", out);
        assert.equal(result.status, 0, result.stderr);
        const index = JSON.parse(readFileSync(out, "utf8"));
        assert.ok(index.items.every((i) => i.stats === null));
        assert.ok(!readFileSync(out, "utf8").includes('"hp"'));
    });

    test("no --from prints how to get a copy and exits 1", () => {
        const result = run();
        assert.equal(result.status, 1);
        assert.match(result.stderr, /never downloads/);
    });

    test("fails loudly on a wrong folder and on too few monsters", () => {
        const wrong = run("--from", tmp, "--out", path.join(tmp, "c.json"));
        assert.equal(wrong.status, 1);
        assert.match(wrong.stderr, /no 5etools data\/ folder/);
        const few = run("--from", fixture, "--out", path.join(tmp, "d.json"));
        assert.equal(few.status, 1);
        assert.match(few.stderr, /only 8 monsters; expected at least 1000/);
    });
});
