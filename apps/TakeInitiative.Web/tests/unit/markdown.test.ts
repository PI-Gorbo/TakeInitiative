import { describe, expect, it } from "vitest";
import { renderNoteMarkdown } from "~/utils/markdown";

describe("renderNoteMarkdown", () => {
    it("renders inline markdown", () => {
        expect(renderNoteMarkdown("**10gp** each")).toBe("<p><strong>10gp</strong> each</p>\n");
        expect(renderNoteMarkdown("_quietly_")).toContain("<em>quietly</em>");
        expect(renderNoteMarkdown("- one\n- two")).toContain("<li>one</li>");
    });

    it("keeps single line breaks", () => {
        expect(renderNoteMarkdown("one\ntwo")).toBe("<p>one<br>\ntwo</p>\n");
    });

    it("escapes raw HTML", () => {
        const out = renderNoteMarkdown('<img src=x onerror="alert(1)"><script>alert(1)</script>');
        expect(out).not.toContain("<img");
        expect(out).not.toContain("<script");
        expect(out).toContain("&lt;script&gt;");
    });

    it("drops script and data links", () => {
        for (const href of ["javascript:alert(1)", "JAVASCRIPT:alert(1)", "vbscript:x", "data:text/html,<b>x</b>"]) {
            const out = renderNoteMarkdown(`[click](${href})`);
            expect(out).not.toContain("<a");
            expect(out.toLowerCase()).not.toContain('href="javascript');
        }
    });

    it("does not render images", () => {
        const out = renderNoteMarkdown("![map](https://example.com/tracker.png)");
        expect(out).not.toContain("<img");
    });

    it("opens links in a new tab without passing anything on", () => {
        const out = renderNoteMarkdown("[map](https://example.com/map)");
        expect(out).toContain('href="https://example.com/map"');
        expect(out).toContain('target="_blank"');
        expect(out).toContain('rel="noopener noreferrer nofollow"');
    });

    it("links bare URLs", () => {
        const out = renderNoteMarkdown("see https://example.com/x");
        expect(out).toContain('<a href="https://example.com/x" target="_blank" rel="noopener noreferrer nofollow">');
    });

    it("renders headings as bold paragraphs", () => {
        expect(renderNoteMarkdown("# Loot")).toBe("<p><strong>Loot</strong></p>\n");
        expect(renderNoteMarkdown("Loot\n===")).toBe("<p><strong>Loot</strong></p>\n");
    });

    it("renders an entry mention as its plain text (the step 15 seam)", () => {
        const out = renderNoteMarkdown("We met @[Gundren](entry:0b7c) on the road.");
        expect(out).toBe("<p>We met Gundren on the road.</p>\n");
    });

    it("keeps formatting inside a mention and leaves other links alone", () => {
        const out = renderNoteMarkdown("@[**Klarg**](entry:1) and [a map](https://example.com)");
        expect(out).toContain("<strong>Klarg</strong> and <a href=");
        expect(out).not.toContain("entry:");
    });
});
