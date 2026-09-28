import { getSchema, type JSONContent } from "@tiptap/core";
import { describe, expect, it } from "vitest";
import { composerExtensions } from "~/utils/editorExtensions";
import { docToText, textToDoc } from "~/utils/editorText";
import { renderNoteMarkdown } from "~/utils/markdown";
import { fromStoredText, toStoredText } from "~/utils/mentions";

const GUNDREN = "11111111-1111-4111-8111-111111111111";
const SILDAR = "22222222-2222-4222-8222-222222222222";
const NEW_ID = "33333333-3333-4333-8333-333333333333";

/** The text, through the editor and back. */
const roundTrip = (text: string, links: Record<string, string> = {}) => docToText(textToDoc({ text, links }));

const env = {
    campaignId: "c",
    resolve: (id: string) => ({ id, kind: "Character" as const }),
};
const render = (stored: string) => renderNoteMarkdown(stored, env);

const text = (value: string, marks: string[] = []): JSONContent =>
    marks.length > 0 ? { type: "text", text: value, marks: marks.map((type) => ({ type })) } : { type: "text", text: value };
const p = (...content: JSONContent[]): JSONContent => (content.length ? { type: "paragraph", content } : { type: "paragraph" });
const doc = (...content: JSONContent[]): JSONContent => ({ type: "doc", content });
const mention = (id: string, raw: string, extra: Partial<{ label: string; isNew: boolean }> = {}): JSONContent => ({
    type: "mention",
    attrs: { id, raw, label: extra.label ?? raw, isNew: extra.isNew ?? false },
});
const list = (...items: JSONContent[][]): JSONContent => ({
    type: "bulletList",
    content: items.map((content) => ({ type: "listItem", content })),
});

describe("textToDoc", () => {
    it("makes one paragraph per line, an empty line an empty paragraph", () => {
        expect(textToDoc({ text: "", links: {} })).toEqual(doc(p()));
        expect(textToDoc({ text: "one\n\ntwo", links: {} })).toEqual(doc(p(text("one")), p(), p(text("two"))));
    });

    it("reads bold and italic as marks", () => {
        expect(textToDoc({ text: "**10gp** each, *quietly*", links: {} })).toEqual(
            doc(p(text("10gp", ["bold"]), text(" each, "), text("quietly", ["italic"])))
        );
        expect(textToDoc({ text: "***both***", links: {} })).toEqual(doc(p(text("both", ["bold", "italic"]))));
    });

    it("leaves what the editor does not draw as its markdown source", () => {
        for (const line of ["# Heading", "> a quote", "[a link](https://x.test/a_b)", "_under_ and __score__", "`a*b*c`", "1. first"]) {
            expect(textToDoc({ text: line, links: {} })).toEqual(doc(p(text(line))));
        }
    });

    it("reads bullet lists, nested two spaces a level", () => {
        expect(textToDoc({ text: "- sword\n  - sharp\n- shield", links: {} })).toEqual(
            doc(list([p(text("sword")), list([p(text("sharp"))])], [p(text("shield"))]))
        );
    });

    it("reads linked mentions and the stored form as mention nodes, and marks new entries", () => {
        const result = textToDoc({
            text: "@[Gundren] met @[Sildar](entry:" + SILDAR + ") and @[Wolf]",
            links: { Gundren: GUNDREN, Wolf: NEW_ID },
            newEntries: [{ id: NEW_ID }],
        });
        expect(result).toEqual(
            doc(
                p(
                    mention(GUNDREN, "Gundren"),
                    text(" met "),
                    mention(SILDAR, "Sildar"),
                    text(" and "),
                    mention(NEW_ID, "Wolf", { isNew: true })
                )
            )
        );
    });

    it("keeps an unlinked @[…] as text, and unescapes a mention's label", () => {
        expect(textToDoc({ text: "@[Nobody]", links: {} })).toEqual(doc(p(text("@[Nobody]"))));
        expect(textToDoc({ text: String.raw`@[R\_2]`, links: { [String.raw`R\_2`]: GUNDREN } })).toEqual(
            doc(p({ ...mention(GUNDREN, String.raw`R\_2`, { label: "R_2" }) }))
        );
    });

    it("lets emphasis wrap a mention", () => {
        expect(textToDoc({ text: "**@[Gundren]** said", links: { Gundren: GUNDREN } })).toEqual(
            doc(p({ ...mention(GUNDREN, "Gundren"), marks: [{ type: "bold" }] }, text(" said")))
        );
    });
});

describe("docToText", () => {
    it("writes marks as markdown, with whitespace outside the markers", () => {
        expect(docToText(doc(p(text("10gp ", ["bold"]), text("each"))))).toEqual({ text: "**10gp** each", links: {} });
        expect(docToText(doc(p(text("a", ["bold"]), text("b", ["bold", "italic"]))))).toEqual({
            text: "**a*b***",
            links: {},
        });
    });

    it("writes lists and nested lists", () => {
        const out = docToText(doc(p(text("Loot:")), list([p(text("sword")), list([p(text("sharp"))])], [p()])));
        expect(out.text).toBe("Loot:\n- sword\n  - sharp\n- ");
    });

    it("escapes a literal * only when it would pair up", () => {
        expect(docToText(doc(p(text("2 * 3 = 6")))).text).toBe("2 * 3 = 6");
        expect(docToText(doc(p(text("a*b")))).text).toBe("a*b");
        expect(docToText(doc(p(text("2*3*4")))).text).toBe(String.raw`2\*3\*4`);
        expect(render(docToText(doc(p(text("2*3*4")))).text)).toBe("<p>2*3*4</p>\n");
    });

    it("escapes a paragraph that would read as a list item", () => {
        expect(docToText(doc(p(text("- not a list")))).text).toBe(String.raw`\- not a list`);
        expect(render(docToText(doc(p(text("* not a list")))).text)).toBe("<p>* not a list</p>\n");
    });

    it("escapes a literal @[…] that would read as a mention", () => {
        const out = docToText(doc(p(mention(GUNDREN, "Gundren"), text(" is not @[Gundren]"))));
        expect(out).toEqual({ text: String.raw`@[Gundren] is not \@[Gundren]`, links: { Gundren: GUNDREN } });
        expect(toStoredText(out.text, out.links)).toBe(`@[Gundren](entry:${GUNDREN}) is not \\@[Gundren]`);
    });

    it("gives a second entry under the same name the stored form", () => {
        const out = docToText(doc(p(mention(GUNDREN, "Gundren"), text(" and "), mention(SILDAR, "Gundren"))));
        expect(out).toEqual({ text: `@[Gundren] and @[Gundren](entry:${SILDAR})`, links: { Gundren: GUNDREN } });
    });
});

// Texts the textarea could have written: each reads into the editor and writes back
// exactly as it was.
const EXACT = [
    "",
    "plain text",
    "one\ntwo\n\nthree",
    "trailing newline\n",
    "  leading spaces",
    "**bold** and *italic* and ***both***",
    "**@[Gundren]** owes *@[Sildar]* 10gp",
    "- one\n- two\n  - nested\n    - deeper\n- three",
    "Loot:\n- sword\n\nThen we left.",
    "2 * 3 = 6, a*b, 5*",
    "# Session recap\n> quoted\n1. first\n2. second",
    "[link](https://example.test/a_b_c) and https://x.test/snake_case",
    "`code *with* stars` after",
    "_underscore italic_ and __underscore bold__",
    String.raw`escaped \*stars\* and \_underscores\_ and \# hash`,
    String.raw`\- not a list`,
    "- - -\n***\n* * *",
    "<b>raw html</b> &amp; entities",
    "~~strike~~ ^sup^",
    "email me@example.test (@[Gundren])",
    "@[Nobody] unlinked",
    `@[Gundren] and @[Gundren](entry:${SILDAR})`,
    "@[Wolf] is new",
    "emoji 🐉 and ünïcödé",
    "C:\\path\\to\\file",
];
const LINKS = { Gundren: GUNDREN, Sildar: SILDAR, Wolf: NEW_ID };

describe("round trips", () => {
    it.each(EXACT)("reads and writes %j exactly", (value) => {
        const out = roundTrip(value, LINKS);
        expect(out.text).toBe(value);
        expect(toStoredText(out.text, out.links)).toBe(toStoredText(value, LINKS));
    });

    // Texts the editor normalizes: the markdown changes, what is drawn does not.
    const NORMALIZED: [string, string][] = [
        ["* star item\n* another", "- star item\n- another"],
        // Only escaped where it would read as a mention (see the docToText tests).
        [String.raw`literal \@[Gundren] not a mention`, "literal @[Gundren] not a mention"],
        ["- a\n    - four-space nested", "- a\n  - four-space nested"],
        [String.raw`a \*lone star`, "a *lone star"],
    ];
    it.each(NORMALIZED)("normalizes %j to %j, drawn the same", (value, expected) => {
        const out = roundTrip(value, LINKS);
        expect(out.text).toBe(expected);
        expect(render(toStoredText(out.text, out.links))).toBe(render(toStoredText(value, LINKS)));
    });

    it("is stable: writing what it read, then reading that again, changes nothing", () => {
        for (const value of [...EXACT, ...NORMALIZED.map(([v]) => v), "*a\nb*", "2*3*4 and \\\\*x"]) {
            const once = roundTrip(value, LINKS);
            expect(roundTrip(once.text, once.links)).toEqual(once);
        }
    });

    it("keeps the stored form: toStoredText(fromStoredText(x)) through the editor is x", () => {
        const stored = [
            `@[Gundren](entry:${GUNDREN}) met @[Sildar](entry:${SILDAR})`,
            `**@[Gundren](entry:${GUNDREN})** and @[Gundren](entry:${SILDAR})`,
            `- @[Gundren](entry:${GUNDREN})\n  - *nested* @[Sildar](entry:${SILDAR})`,
            String.raw`@[R\_2](entry:${GUNDREN}) and \# escaped`,
            "no mentions *at all*",
        ];
        for (const value of stored) {
            const composer = fromStoredText(value);
            const out = roundTrip(composer.text, composer.links);
            expect(toStoredText(out.text, out.links)).toBe(value);
        }
    });

    it("draws every text the same after the trip", () => {
        for (const value of [...EXACT, "*a\nb*", "2*3*4"]) {
            const out = roundTrip(value, LINKS);
            expect(render(toStoredText(out.text, out.links))).toBe(render(toStoredText(value, LINKS)));
        }
    });
});

describe("the editor's schema", () => {
    const schema = getSchema(composerExtensions());

    it("accepts every document the text becomes, and writes it back the same", () => {
        for (const value of [...EXACT, "- a\n    - b\n      - c\n- d", "text\n- item\ntext"]) {
            const node = schema.nodeFromJSON(textToDoc({ text: value, links: LINKS, newEntries: [{ id: NEW_ID }] }));
            expect(() => node.check()).not.toThrow();
            expect(docToText(node.toJSON())).toEqual(roundTrip(value, LINKS));
        }
    });

    it("keeps a mention's attributes", () => {
        const node = schema.nodeFromJSON(textToDoc({ text: "@[Wolf]", links: LINKS, newEntries: [{ id: NEW_ID }] }));
        expect(node.firstChild!.firstChild!.attrs).toMatchObject({ id: NEW_ID, label: "Wolf", raw: "Wolf", isNew: true });
    });
});
