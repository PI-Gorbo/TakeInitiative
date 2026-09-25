import { describe, expect, it } from "vitest";
// The case list the API's MentionParserTests also run (15b), so the two parsers
// cannot drift apart.
import fixture from "../../../TakeInitiative.Api.Tests/Fixtures/mentions.json";
import type { EntryKind } from "~/utils/api/types";
import { mentionedEntryIds, noteMentions, renderNoteMarkdown, type NoteMarkdownEnv } from "~/utils/markdown";

type Case = { name: string; text: string; mentions: { entryId: string; text: string }[] };
const cases = (fixture as { cases: Case[] }).cases;

const CAMPAIGN = "c0000000-0000-0000-0000-000000000001";
const GUNDREN = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a001";
const KLARG = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a003";

/** A directory that holds every id (or only `visible`), all Characters. */
function env(visible?: string[], kind: EntryKind = "Character"): NoteMarkdownEnv {
    return {
        campaignId: CAMPAIGN,
        resolve: (entryId) => (!visible || visible.includes(entryId) ? { id: entryId, kind } : undefined),
    };
}

const chipCount = (html: string) => (html.match(/<a class="mention"/g) ?? []).length;

describe("the shared mention fixture", () => {
    it("has cases", () => expect(cases.length).toBeGreaterThanOrEqual(12));

    for (const c of cases) {
        it(`parses: ${c.name}`, () => {
            expect(noteMentions(c.text)).toEqual(c.mentions);
        });

        it(`draws one chip per mention: ${c.name}`, () => {
            const html = renderNoteMarkdown(c.text, env());
            expect(chipCount(html)).toBe(c.mentions.length);
            for (const m of c.mentions) expect(html).toContain(`data-entry-id="${m.entryId}"`);
        });
    }
});

describe("mention chips", () => {
    it("links to the entry page with the kind's icon, and drops the @", () => {
        const html = renderNoteMarkdown(`We met @[Gundren](entry:${GUNDREN}) on the road.`, env(undefined, "Place"));
        expect(html).toBe(
            `<p>We met <a class="mention" data-entry-id="${GUNDREN}" data-kind="Place" href="/app/campaigns/${CAMPAIGN}/wiki/${GUNDREN}">` +
                `<span class="mention-icon" aria-hidden="true">📍</span>Gundren</a> on the road.</p>\n`
        );
    });

    it("draws a mention of an entry the viewer cannot see as its plain text, with no hint", () => {
        const html = renderNoteMarkdown(`@[Gundren](entry:${GUNDREN}) and @[Klarg](entry:${KLARG})`, env([KLARG]));
        expect(html).toContain("<p>Gundren and <a class=\"mention\"");
        expect(html).not.toContain(GUNDREN);
        expect(chipCount(html)).toBe(1);
    });

    it("links to the id the directory resolves to (a merge target in 15g)", () => {
        const resolve: NoteMarkdownEnv["resolve"] = (id) => (id === GUNDREN ? { id: KLARG, kind: "Character" } : undefined);
        const html = renderNoteMarkdown(`@[Gundren](entry:${GUNDREN})`, { campaignId: CAMPAIGN, resolve });
        expect(html).toContain(`href="/app/campaigns/${CAMPAIGN}/wiki/${KLARG}"`);
    });

    it("resolves upper-case ids by their lower-case form", () => {
        const html = renderNoteMarkdown(`@[Gundren](entry:${GUNDREN.toUpperCase()})`, env([GUNDREN]));
        expect(chipCount(html)).toBe(1);
    });

    it("keeps the chip text escaped", () => {
        const html = renderNoteMarkdown(`@[<b>x</b>](entry:${GUNDREN})`, env());
        expect(html).toContain("&lt;b&gt;x&lt;/b&gt;</a>");
    });

    it("leaves ordinary links opening in a new tab", () => {
        const html = renderNoteMarkdown(`@[Gundren](entry:${GUNDREN}) [map](https://example.com)`, env());
        expect(html).toContain('<a href="https://example.com" target="_blank"');
        expect(html).not.toMatch(/class="mention"[^>]*target=/);
    });
});

describe("mentionedEntryIds", () => {
    it("is the distinct ids in order of first mention", () => {
        const several = cases.find((c) => c.name.startsWith("several"))!;
        expect(mentionedEntryIds(several.text)).toEqual([GUNDREN, "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a002"]);
    });
});
