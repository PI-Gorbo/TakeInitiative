import { describe, expect, it } from "vitest";
import type { ArticleBlock, CampaignMember, EntryChange, EntryList, EntrySummary } from "~/utils/api/types";
import { restoreEditorBlocks, articleSaveBody } from "~/utils/article";
import {
    canAssignClaim,
    canClaimEntry,
    canReadStats,
    canUnclaimEntry,
    canWriteStats,
    currentVersionIndex,
    describeChange,
    entryDirectory,
    entryVisibleTo,
    mergeLosers,
    mergeProblem,
    mergeRevealsNothing,
    parseStatsForm,
    resolveEntry,
    statsLabel,
} from "~/utils/entries";
import { applyMerge } from "~/utils/entryCache";
import { renderNoteMarkdown } from "~/utils/markdown";

// 15g's pure rules: the directory after a merge, the merge guard (the API's EntryMerge),
// claims and stats (the API's EntryStats), history lines, and restoring a version.

const GUNDREN = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a001";
const OLD = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a002";
const OLDER = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a003";

const DM: CampaignMember = { memberId: "dm", userId: "u-dm", username: "Alex", role: "DM", joinedAt: "", isOwner: true };
const SAM: CampaignMember = { memberId: "sam", userId: "u-sam", username: "Sam", role: "Player", joinedAt: "", isOwner: false };
const PRIYA: CampaignMember = { memberId: "priya", userId: "u-priya", username: "Priya", role: "Player", joinedAt: "", isOwner: false };
const MEMBERS = [DM, SAM, PRIYA];

function summary(id: string, name: string, extra: Partial<EntrySummary> = {}): EntrySummary {
    return {
        id,
        name,
        kind: "Character",
        aliases: [],
        mergedFromIds: [],
        visibility: "Everyone",
        editAccess: "Anyone",
        creatorMemberId: "sam",
        createdAt: "2026-09-01T00:00:00Z",
        updatedAt: "2026-09-01T00:00:00Z",
        ...extra,
    };
}
const list = (...entries: EntrySummary[]): EntryList => ({
    entries: entries.map((entry) => ({ entry, mentionCount: 1, lastMentionedAt: null })),
});

describe("the directory after a merge", () => {
    it("resolves a merged id to its target, and lists only live entries", () => {
        const directory = entryDirectory(list(summary(GUNDREN, "Gundren Rockseeker", { mergedFromIds: [OLD, OLDER] })));
        expect(resolveEntry(directory, OLD)?.id).toBe(GUNDREN);
        expect(resolveEntry(directory, OLDER.toUpperCase())?.id).toBe(GUNDREN);
        expect(directory.items.map((i) => i.entry.id)).toEqual([GUNDREN]);
    });

    it("draws an old mention as a chip to the target, keeping its text (invariant 6)", () => {
        const directory = entryDirectory(list(summary(GUNDREN, "Gundren Rockseeker", { mergedFromIds: [OLD] })));
        const html = renderNoteMarkdown(`We met @[Gundren](entry:${OLD}).`, {
            campaignId: "c1",
            resolve: (id) => resolveEntry(directory, id),
        });
        expect(html).toContain(`href="/app/campaigns/c1/wiki/${GUNDREN}"`);
        expect(html).toContain("Gundren</a>");
    });

    it("applyMerge drops the merged entry and gives its ids to the target", () => {
        const before = list(
            summary(GUNDREN, "Gundren Rockseeker"),
            summary(OLD, "Gundren", { mergedFromIds: [OLDER] })
        );
        const after = applyMerge(before, OLD, GUNDREN)!;
        expect(after.entries.map((i) => i.entry.id)).toEqual([GUNDREN]);
        expect(after.entries[0].entry.mergedFromIds).toEqual([OLD, OLDER]);
        expect(after.entries[0].mentionCount).toBe(1);
        // Idempotent: the target's own push may already carry the ids.
        expect(applyMerge(after, OLD, GUNDREN)!.entries[0].entry.mergedFromIds).toEqual([OLD, OLDER]);
        expect(applyMerge(undefined, OLD, GUNDREN)).toBeUndefined();
    });
});

describe("the merge guard", () => {
    const glasstaff = summary(OLD, "Glasstaff", { visibility: "DM", creatorMemberId: "dm" });
    const iarno = summary(GUNDREN, "Iarno", { creatorMemberId: "dm" });

    it("agrees with who can see what", () => {
        expect(entryVisibleTo(glasstaff, DM)).toBe(true);
        expect(entryVisibleTo(glasstaff, SAM)).toBe(false);
        expect(entryVisibleTo(summary(OLD, "Mine", { visibility: "Me" }), SAM)).toBe(true);
        expect(entryVisibleTo(summary(OLD, "Mine", { visibility: "Me" }), DM)).toBe(false);
    });

    it("refuses a DM entry into an Everyone one, and allows the other way with a warning", () => {
        expect(mergeRevealsNothing(glasstaff, iarno, MEMBERS)).toBe(false);
        expect(mergeProblem(glasstaff, iarno, MEMBERS)).toContain("Change visibility first");
        expect(mergeProblem(iarno, glasstaff, MEMBERS)).toBeNull();
        expect(mergeLosers(iarno, glasstaff, MEMBERS).map((m) => m.username)).toEqual(["Sam", "Priya"]);
    });

    it("keeps a player character with its claimer", () => {
        const pc = summary(OLD, "Tordek", { claimedByMemberId: "sam" });
        expect(mergeProblem(pc, summary(GUNDREN, "Phandalin", { kind: "Place" }), MEMBERS)).toContain("Character");
        expect(mergeProblem(pc, summary(GUNDREN, "Lidda", { claimedByMemberId: "priya" }), MEMBERS)).toContain(
            "different members"
        );
        expect(mergeProblem(pc, summary(GUNDREN, "Tordek the dwarf"), MEMBERS)).toBeNull();
    });

    it("refuses more than 20 aliases", () => {
        const into = summary(GUNDREN, "Gundren", { aliases: Array.from({ length: 19 }, (_, i) => `a${i}`) });
        expect(mergeProblem(summary(OLD, "Rockseeker"), into, MEMBERS)).toBeNull();
        expect(mergeProblem(summary(OLD, "Rockseeker", { aliases: ["b"] }), into, MEMBERS)).toContain("21 aliases");
        // Duplicates of what is there do not count.
        expect(mergeProblem(summary(OLD, "a1", { aliases: ["GUNDREN", "b"] }), into, MEMBERS)).toBeNull();
    });
});

describe("claims and stats", () => {
    const sam = { memberId: "sam", isDm: false };
    const priya = { memberId: "priya", isDm: false };
    const dm = { memberId: "dm", isDm: true };
    const npc = summary(GUNDREN, "Klarg");
    const pc = summary(GUNDREN, "Tordek", { claimedByMemberId: "sam" });

    it("follows the API's table", () => {
        expect(canClaimEntry(npc)).toBe(true);
        expect(canClaimEntry(pc)).toBe(false);
        expect(canClaimEntry(summary(GUNDREN, "Phandalin", { kind: "Place" }))).toBe(false);
        expect(canUnclaimEntry(pc, sam)).toBe(true);
        expect(canUnclaimEntry(pc, priya)).toBe(false);
        expect(canUnclaimEntry(pc, dm)).toBe(true);
        expect(canAssignClaim(npc, dm)).toBe(true);
        expect(canAssignClaim(npc, sam)).toBe(false);
    });

    it("keeps an unclaimed entry's stats for the DMs", () => {
        expect(canReadStats(npc, sam)).toBe(false);
        expect(canReadStats(npc, dm)).toBe(true);
        expect(canWriteStats(npc, sam)).toBe(false);
        expect(canReadStats(pc, priya)).toBe(true);
        expect(canWriteStats(pc, sam)).toBe(true);
        expect(canWriteStats(pc, priya)).toBe(false);
        expect(canWriteStats(pc, dm)).toBe(true);
        expect(canReadStats(summary(GUNDREN, "Wagon", { kind: "Item" }), dm)).toBe(false);
    });

    it("reads the form: blank is none, AC is 0 to 99", () => {
        expect(parseStatsForm({ initiativeRoll: " 1d20+2 ", maxHp: "", ac: "17" })).toEqual({
            body: { initiativeRoll: "1d20+2", maxHp: null, ac: 17 },
            errors: {},
        });
        expect(parseStatsForm({ initiativeRoll: "", maxHp: "", ac: "" }).body).toEqual({
            initiativeRoll: null,
            maxHp: null,
            ac: null,
        });
        expect(parseStatsForm({ initiativeRoll: "", maxHp: "", ac: "100" }).errors.ac).toContain("0 and 99");
        expect(parseStatsForm({ initiativeRoll: "", maxHp: "", ac: "1.5" }).body).toBeNull();
    });

    it("labels a stat line", () => {
        expect(statsLabel({ initiativeRoll: "1d20+2", maxHp: "3d10", ac: 17 })).toBe("Initiative 1d20+2 · HP 3d10 · AC 17");
        expect(statsLabel({ ac: 0 })).toBe("AC 0");
        expect(statsLabel(null)).toBe("");
    });
});

describe("history", () => {
    const block = (id: string, text: string, extra: Partial<ArticleBlock> = {}): ArticleBlock => ({
        id,
        text,
        visibility: "Everyone",
        ownerMemberId: "sam",
        ...extra,
    });

    it("describes each change", () => {
        const nameOf = (id: string) => (id === "sam" ? "Sam" : id);
        const line = (change: EntryChange) => describeChange(change, nameOf);
        expect(line({ type: "Renamed", name: "Gundren Rockseeker" })).toBe('renamed it to "Gundren Rockseeker"');
        expect(line({ type: "Merged", name: "Gundren", blocks: [] })).toBe('merged "Gundren" into it');
        expect(line({ type: "Claimed", memberId: "sam" })).toBe("made it Sam's player character");
        expect(line({ type: "StatsChanged", stats: null })).toBe("cleared the stats");
        expect(line({ type: "Created", kind: "Place", visibility: "DM" })).toBe("created it as a Place (🔒 DM)");
    });

    it("the last article version is the current one", () => {
        const items = [
            { change: { type: "Created" } },
            { change: { type: "ArticleEdited", blocks: [] } },
            { change: { type: "QuotePromoted", blocks: [] } },
            { change: { type: "Renamed", name: "x" } },
        ] as { change: EntryChange }[];
        expect(currentVersionIndex(items)).toBe(2);
        expect(currentVersionIndex([{ change: { type: "Created" } }])).toBe(-1);
    });

    it("restores a version: existing blocks keep their id, gone ones are sent without one", () => {
        let n = 0;
        const newKey = () => `k${++n}`;
        const current = [block("a", "Vandalised."), block("s", "DM secret", { visibility: "DM", ownerMemberId: "dm" })];
        const version = [
            block("a", "Dwarf prospector."),
            block("gone", "A removed line."),
            block("s", "Old secret", { visibility: "Everyone", ownerMemberId: "dm" }),
        ];

        const restored = restoreEditorBlocks(version, current, { memberId: "sam", isDm: false }, newKey);

        expect(restored.map((b) => b.id)).toEqual(["a", null, "s"]);
        expect(restored[1].ownerMemberId).toBe("sam");
        // Sam may not change the DM's block's visibility, so it keeps the current one.
        expect(restored[2].visibility).toBe("DM");
        const body = articleSaveBody("etag", restored);
        expect(body.blocks).toEqual([
            { id: "a", text: "Dwarf prospector.\n\nA removed line.", visibility: "Everyone" },
            { id: "s", text: "Old secret", visibility: "DM" },
        ]);
    });
});
