import { describe, expect, it } from "vitest";
import type { EntryLink, EntrySummary, KnowledgeBaseItem } from "~/utils/api/types";
import type { EntryViewer } from "~/utils/entries";
import {
    DND_BEYOND_LABEL,
    LINK_LABEL_MAX,
    LINK_URL_MAX,
    STALE_LINK_DETAIL,
    canAddLinks,
    canReadLinks,
    canWriteLinks,
    displayHost,
    dndBeyondLink,
    dndBeyondSheetUrl,
    dndBeyondUrlError,
    isAllowedLinkUrl,
    isPendingLink,
    linkAnchor,
    linkDetail,
    linkLabelError,
    linkTitle,
    linkUrlError,
    linksItem,
    linksShowHigh,
    pendingExternalLink,
    pendingKnowledgeBaseLink,
} from "~/utils/links";

// ── Fixtures ─────────────────────────────────────────────────────────────────

const DM: EntryViewer = { memberId: "dm", isDm: true };
const PLAYER: EntryViewer = { memberId: "player", isDm: false };
const OTHER: EntryViewer = { memberId: "other", isDm: false };

type Subject = Pick<EntrySummary, "kind" | "claimedByMemberId" | "creatorMemberId" | "editAccess">;

const entry = (extra: Partial<Subject> = {}): Subject => ({
    kind: "Character",
    claimedByMemberId: null,
    creatorMemberId: "dm",
    editAccess: "Anyone",
    ...extra,
});

const external = (extra: Partial<EntryLink> = {}): EntryLink => ({
    id: "11111111-1111-1111-1111-111111111111",
    kind: "External",
    addedAt: "2026-01-01T00:00:00Z",
    addedByMemberId: "player",
    url: "https://docs.google.com/document/d/abc",
    label: "my notes on running it",
    hasStatBlock: false,
    stale: false,
    ...extra,
});

const reference = (extra: Partial<EntryLink> = {}): EntryLink => ({
    id: "22222222-2222-2222-2222-222222222222",
    kind: "KnowledgeBase",
    addedAt: "2026-01-02T00:00:00Z",
    addedByMemberId: "dm",
    provider: "5etools",
    providerLabel: "5eTools",
    itemId: "monster_beholder_mm",
    name: "Beholder",
    detail: "Monster · CR 13 · MM",
    url: "https://5e.tools/bestiary.html#beholder_mm",
    bookTitle: "Monster Manual (2014)",
    hasStatBlock: false,
    stale: false,
    ...extra,
});

const item = (extra: Partial<KnowledgeBaseItem> = {}): KnowledgeBaseItem => ({
    provider: "5etools",
    providerLabel: "5eTools",
    id: "monster_beholder_mm",
    name: "Beholder",
    category: "Monster",
    label: "CR 13 · Large Aberration",
    book: "MM",
    bookTitle: "Monster Manual (2014)",
    page: 28,
    url: "https://5e.tools/bestiary.html#beholder_mm",
    imageUrl: null,
    ...extra,
});

// ── Who reads and writes (the API's EntryLinks) ──────────────────────────────

describe("who may read links", () => {
    it("is Source's rule: an unclaimed Character's links are the DMs' alone", () => {
        expect(canReadLinks(entry(), DM)).toBe(true);
        expect(canReadLinks(entry(), PLAYER)).toBe(false);
    });

    it("shows a claimed Character's links to everyone who can see it", () => {
        const claimed = entry({ claimedByMemberId: "player" });
        expect(canReadLinks(claimed, PLAYER)).toBe(true);
        expect(canReadLinks(claimed, OTHER)).toBe(true);
        expect(canReadLinks(claimed, DM)).toBe(true);
    });

    it("hides nothing on the other kinds", () => {
        for (const kind of ["Place", "Faction", "Item", "Event", "Other"] as const) {
            expect(canReadLinks(entry({ kind }), PLAYER)).toBe(true);
        }
    });
});

describe("who may write links", () => {
    it("is the entry's editors", () => {
        expect(canWriteLinks(entry(), DM)).toBe(true);
        expect(canWriteLinks(entry({ creatorMemberId: "player" }), PLAYER)).toBe(true);
        expect(canWriteLinks(entry({ editAccess: "Anyone" }), OTHER)).toBe(true);
        expect(canWriteLinks(entry({ editAccess: "OnlyMe" }), OTHER)).toBe(false);
    });

    it("plus a claimed Character's player, even with edit access restricted", () => {
        // The case 27d's permission fix exists for: a DM creates the NPC, a player claims it,
        // the DM restricts edit access. The player keeps their own sheet link.
        const mine = entry({ claimedByMemberId: "player", creatorMemberId: "dm", editAccess: "OnlyMe" });
        expect(canWriteLinks(mine, PLAYER)).toBe(true);
        // Another player is still out: it is the claimer's clause, not every player's.
        expect(canWriteLinks(mine, OTHER)).toBe(false);
        // And it is only about links: the article keeps `canEditEntry`, which this is not.
    });

    it("does not follow an unclaimed Character, nor another kind, to its claimer", () => {
        expect(canWriteLinks(entry({ editAccess: "OnlyMe" }), PLAYER)).toBe(false);
        expect(canWriteLinks(entry({ kind: "Place", claimedByMemberId: "player", editAccess: "OnlyMe" }), PLAYER)).toBe(
            false
        );
    });

    it("offers [+] only to someone who would also read what they added", () => {
        // A player may edit an unclaimed Character whose edit access is Anyone, and the API would
        // take the link — but the read rule would then hide it, so the web does not offer it.
        const npc = entry({ editAccess: "Anyone" });
        expect(canWriteLinks(npc, PLAYER)).toBe(true);
        expect(canReadLinks(npc, PLAYER)).toBe(false);
        expect(canAddLinks(npc, PLAYER)).toBe(false);
        expect(canAddLinks(npc, DM)).toBe(true);
    });
});

describe("where the section sits", () => {
    it("is high on a phone for a claimed Character only", () => {
        expect(linksShowHigh(entry({ claimedByMemberId: "player" }))).toBe(true);
        expect(linksShowHigh(entry())).toBe(false);
        expect(linksShowHigh(entry({ kind: "Place" }))).toBe(false);
    });
});

// ── A url this app will store or render ──────────────────────────────────────

describe("the url allowlist", () => {
    it("takes http and https only", () => {
        expect(isAllowedLinkUrl("https://example.com/a")).toBe(true);
        expect(isAllowedLinkUrl("http://example.com/a")).toBe(true);
    });

    it("refuses every other scheme, and anything that is not absolute", () => {
        for (const url of [
            "javascript:alert(1)",
            "data:text/html,<script>alert(1)</script>",
            "file:///etc/passwd",
            "ftp://example.com/x",
            "mailto:someone@example.com",
            "/not/absolute",
            "example.com/no-scheme",
            "",
            "   ",
        ]) {
            expect(isAllowedLinkUrl(url), url).toBe(false);
        }
    });

    it("caps the length at the API's", () => {
        const fits = `https://example.com/${"a".repeat(LINK_URL_MAX - "https://example.com/".length)}`;
        expect(fits).toHaveLength(LINK_URL_MAX);
        expect(isAllowedLinkUrl(fits)).toBe(true);
        expect(isAllowedLinkUrl(`${fits}a`)).toBe(false);
    });

    it("says why, in the API's words", () => {
        expect(linkUrlError("")).toBe("Paste a link.");
        expect(linkUrlError("javascript:alert(1)")).toContain("http:// or https://");
        expect(linkUrlError("https://example.com")).toBeNull();
    });
});

describe("a label", () => {
    it("is required, trimmed, and capped", () => {
        expect(linkLabelError("")).toBe("Give the link a label.");
        expect(linkLabelError("   ")).toBe("Give the link a label.");
        expect(linkLabelError("x".repeat(LINK_LABEL_MAX))).toBeNull();
        expect(linkLabelError(`  ${"x".repeat(LINK_LABEL_MAX)}  `)).toBeNull();
        expect(linkLabelError("x".repeat(LINK_LABEL_MAX + 1))).toContain(`${LINK_LABEL_MAX} characters`);
    });
});

describe("the display host", () => {
    it("drops www. and keeps the rest", () => {
        expect(displayHost("https://www.dndbeyond.com/characters/1")).toBe("dndbeyond.com");
        expect(displayHost("https://docs.google.com/document/d/abc")).toBe("docs.google.com");
        expect(displayHost("javascript:alert(1)")).toBe("");
        expect(displayHost(null)).toBe("");
    });
});

// ── A row ────────────────────────────────────────────────────────────────────

describe("a row", () => {
    it("names an external link by its label, and a reference one by its row's name", () => {
        expect(linkTitle(external())).toBe("my notes on running it");
        expect(linkTitle(reference())).toBe("Beholder");
    });

    it("falls back to the host and to the stored id rather than showing nothing", () => {
        expect(linkTitle(external({ label: null }))).toBe("docs.google.com");
        expect(linkTitle(external({ label: null, url: null }))).toBe("A link");
        expect(linkTitle(reference({ name: null }))).toBe("monster_beholder_mm");
    });

    it("renders a label as the plain text it is, never as markdown", () => {
        const label = "**bold** [x](javascript:alert(1))";
        expect(linkTitle(external({ label }))).toBe(label);
    });

    it("details an external link with its host and a reference one with the corpus's line", () => {
        expect(linkDetail(external())).toBe("docs.google.com");
        expect(linkDetail(reference())).toBe("Monster · CR 13 · MM");
    });

    it("opens in a new tab, with noopener noreferrer", () => {
        expect(linkAnchor(external())).toEqual({
            href: "https://docs.google.com/document/d/abc",
            target: "_blank",
            rel: "noopener noreferrer",
        });
    });

    it("never puts a url the allowlist would refuse into an href", () => {
        expect(linkAnchor({ url: "javascript:alert(1)" })).toBeNull();
    });
});

describe("a stale knowledge-base link", () => {
    // 27's Layouts, which the Goal paragraph contradicted: there is no ↗, because the row has
    // gone from the corpus and `url` comes back null, so there is nowhere honest to send anyone.
    const stale = reference({ stale: true, url: null, detail: null });

    it("says so in place of its detail line", () => {
        expect(linkDetail(stale)).toBe(STALE_LINK_DETAIL);
    });

    it("keeps its last-known name, so the member can tell which link went", () => {
        expect(linkTitle(stale)).toBe("Beholder");
    });

    it("has no anchor at all, and so no ↗", () => {
        expect(linkAnchor(stale)).toBeNull();
    });

    it("is still a row with an id, so it is still removable", () => {
        expect(isPendingLink(stale)).toBe(false);
        expect(stale.id).toBeTruthy();
    });
});

describe("an optimistic row", () => {
    it("is marked pending, so it offers no Remove until the server has answered", () => {
        const pending = pendingExternalLink("  https://example.com/map  ", "  the map  ", "player");
        expect(isPendingLink(pending)).toBe(true);
        expect(pending.url).toBe("https://example.com/map");
        expect(pending.label).toBe("the map");
        expect(pending.addedByMemberId).toBe("player");
    });

    it("draws a reference row from the picked item", () => {
        const pending = pendingKnowledgeBaseLink(item(), "dm");
        expect(isPendingLink(pending)).toBe(true);
        expect(pending.kind).toBe("KnowledgeBase");
        expect(pending.provider).toBe("5etools");
        expect(pending.itemId).toBe("monster_beholder_mm");
        expect(pending.detail).toBe("Monster · CR 13 · Large Aberration");
        expect(pending.stale).toBe(false);
    });

    it("gives every row its own id, so two adds are two rows", () => {
        const a = pendingExternalLink("https://example.com/1", "one", "player");
        const b = pendingExternalLink("https://example.com/2", "two", "player");
        expect(a.id).not.toBe(b.id);
    });
});

describe("the picker's duplicate mark", () => {
    it("matches the provider case-insensitively and the id exactly, as the API does", () => {
        expect(linksItem([reference()], item())).toBe(true);
        expect(linksItem([reference({ provider: "5eTOOLS" })], item())).toBe(true);
        expect(linksItem([reference()], item({ id: "monster_other_mm" }))).toBe(false);
        expect(linksItem([external()], item())).toBe(false);
    });
});

// ── D&D Beyond (27e) ─────────────────────────────────────────────────────────

describe("a D&D Beyond sheet url", () => {
    const canonical = "https://www.dndbeyond.com/characters/12345678";

    it("reduces every accepted shape to the canonical sheet url", () => {
        for (const url of [
            "https://www.dndbeyond.com/characters/12345678",
            "https://dndbeyond.com/characters/12345678",
            "http://www.dndbeyond.com/characters/12345678",
            "https://www.dndbeyond.com/characters/12345678/",
            "https://www.dndbeyond.com/characters/12345678/thorin",
            "https://www.dndbeyond.com/characters/12345678?a=1#top",
            "https://www.dndbeyond.com/profile/sam/characters/12345678",
            "https://ddb.ac/characters/12345678/thorin",
            "  https://www.dndbeyond.com/characters/12345678  ",
        ]) {
            expect(dndBeyondSheetUrl(url), url).toBe(canonical);
        }
    });

    it("refuses anything else", () => {
        for (const url of [
            "",
            "   ",
            "https://example.com/characters/12345678",
            "https://dndbeyond.com.evil.test/characters/1",
            "http://user@www.dndbeyond.com/characters/1",
            "https://www.dndbeyond.com:8443/characters/1",
            "https://www.dndbeyond.com/campaigns/12345678",
            "https://www.dndbeyond.com/characters/12345678/builder/class",
            "https://www.dndbeyond.com/characters/1234567890123",
            "https://www.dndbeyond.com/characters/abc",
            "https://www.dndbeyond.com/characters",
            "javascript:alert(1)",
            `https://www.dndbeyond.com/characters/1/${"a".repeat(600)}`,
        ]) {
            expect(dndBeyondSheetUrl(url), url).toBeNull();
        }
    });

    it("says what to do when it is not one", () => {
        expect(dndBeyondUrlError("")).toContain("Paste the address");
        expect(dndBeyondUrlError("https://example.com/x")).toContain("isn't a D&D Beyond character link");
        expect(dndBeyondUrlError("https://ddb.ac/characters/1/x")).toBeNull();
    });

    it("is an ordinary external link, found by its host rather than a flag", () => {
        const sheet = external({ url: canonical, label: DND_BEYOND_LABEL });
        expect(DND_BEYOND_LABEL).toBe("D&D Beyond sheet");
        expect(dndBeyondLink([reference(), sheet])?.id).toBe(sheet.id);
        expect(dndBeyondLink([reference(), external()])).toBeUndefined();
    });
});
