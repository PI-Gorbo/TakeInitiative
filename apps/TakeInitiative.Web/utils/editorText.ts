// The composer's text as a TipTap document, and back (step 17, the rich text box). Pure, so it is unit
// tested without a DOM; `components/Composer/Editor.vue` wires it to the editor.
//
// The composer still works on the text it always had (14d, 15d): markdown, where a
// mention reads `@[Name]` and its entry id lives beside the text in `links`. The
// editor only changes how that text is *written*: bold, italic, bullet lists and
// mentions are drawn instead of typed as markup. So the draft, the character count,
// `toStoredText` and the API are unchanged, and a note posted from the editor is the
// same markdown the textarea would have sent.
//
// The model is line based, as the textarea was:
// - every line of the text is one block: a paragraph, or a bullet list item (`- `,
//   `* ` or `+ `, nested two spaces per level). An empty line is an empty paragraph,
//   so Enter in the editor is exactly one "\n" in the text;
// - within a line, `**bold**` and `*italic*` become marks and `@[Name]` (linked) or
//   `@[Name](entry:<id>)` a mention node. Inline parsing is markdown-it's own emphasis
//   rule, so the editor agrees with the renderer (`utils/markdown.ts`) about what is
//   emphasis;
// - everything else stays as its markdown source (`# heading`, `[link](url)`,
//   `` `code` ``, `_underscores_`, `> quotes`): the editor shows it as typed and the
//   renderer draws it, just as with the textarea.
//
// Writing the text back escapes only what would otherwise change meaning: a literal
// `*` that would pair up into emphasis, a literal `@[` that would read as a mention,
// and a paragraph that starts like a list item. Each line is parsed again to check,
// so text that needs no escape is written exactly as it was read.
import type { JSONContent } from "@tiptap/core";
import MarkdownIt, { type Token } from "markdown-it";
import { escapeMentionText, unescapeMentionText } from "./mentions";

/** The mention node's name in the editor's schema. */
export const MENTION_NODE = "mention";

/**
 * A mention node's attributes. `raw` is the bracket text as written (escapes
 * included), so the text reads back exactly; `label` is what the chip shows. `isNew`
 * marks an entry made by Create that the note will create when it is posted (15d).
 */
export type MentionAttrs = { id: string | null; label: string; raw: string; isNew: boolean };

type Mark = "bold" | "italic";
const MARK_ORDER: readonly Mark[] = ["bold", "italic"];
const MARKER: Record<Mark, string> = { bold: "**", italic: "*" };

type Inline =
    | { kind: "text"; text: string; marks: Mark[] }
    | { kind: "mention"; id: string; raw: string; marks: Mark[] };

type Line = { kind: "paragraph"; inline: Inline[] } | { kind: "item"; depth: number; inline: Inline[] };

// ── Lines ────────────────────────────────────────────────────────────────────

const LIST_LINE = /^( *)([-*+]) (.*)$/;
// `- - -` and `* * *` are rules, not list items.
const THEMATIC_BREAK = /^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$/;
// A paragraph that starts like a list item is written with its marker escaped.
const ESCAPED_MARKER = /^( *)\\([-*+] .*)$/;

const isListLine = (line: string) => LIST_LINE.test(line) && !THEMATIC_BREAK.test(line);

/** One nesting level per two spaces, as the editor writes it. */
const INDENT = "  ";

// ── Parsing ──────────────────────────────────────────────────────────────────

// markdown-it with only what the editor draws: emphasis, and backslash escapes. Code
// spans are parsed only so the `*` inside one stays source text.
const md = new MarkdownIt("zero", { html: false }).enable(["escape", "emphasis", "backticks"]).disable(["text_join"]);

// Stand-ins for a mention and an escaped `\@[` while markdown-it reads the line. Both
// are Unicode symbols, so emphasis around them flanks as it does around `@[…]`.
const MENTION_MARK = "￼";
const AT_MARK = "�";

const GUID = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
const BRACKET_TEXT = String.raw`(?:\\.|[^\\\[\]\n])*`;
/** `@[text]`, or `@[text](entry:<id>)` (the stored form). */
const MENTION_TOKEN = new RegExp(String.raw`@\[(${BRACKET_TEXT})\](?:\(entry:(${GUID})\))?`, "g");

function isEscaped(text: string, index: number): boolean {
    let slashes = 0;
    for (let i = index - 1; i >= 0 && text[i] === "\\"; i--) slashes++;
    return slashes % 2 === 1;
}

type ParseContext = { links: Readonly<Record<string, string>> };

/** One line's inline content. */
function parseInline(line: string, context: ParseContext): Inline[] {
    const mentions: { id: string; raw: string }[] = [];
    if (line.includes(MENTION_MARK) || line.includes(AT_MARK)) return literalLine(line, context);

    // Mentions become stand-ins first, so emphasis may wrap them.
    let src = "";
    let at = 0;
    for (const match of line.matchAll(MENTION_TOKEN)) {
        const start = match.index;
        const end = start + match[0].length;
        if (isEscaped(line, start)) {
            // `\@[`: a literal `@[`, which the writer escapes (see `escapeLiteral`).
            src += line.slice(at, start - 1) + AT_MARK;
            at = start + 1;
            continue;
        }
        const raw = match[1]!;
        const id = match[2] ?? (line[end] === "(" ? undefined : context.links[raw]);
        if (!id) continue;
        src += line.slice(at, start) + MENTION_MARK;
        mentions.push({ id, raw });
        at = end;
    }
    src += line.slice(at);

    const pieces = walkTokens(src, md.parseInline(src, {})[0]?.children ?? []);
    if (!pieces) return literalLine(line, context);
    return mergeText(expandMarks(pieces, mentions));
}

/** markdown-it's tokens as text with marks. Undefined for anything unexpected. */
function walkTokens(src: string, tokens: Token[]): { text: string; marks: Mark[] }[] | undefined {
    const out: { text: string; marks: Mark[] }[] = [];
    const marks: Mark[] = [];
    // What each open emphasis did: a mark, or `_` source text written back as is.
    const opened: (Mark | string)[] = [];
    let cursor = 0;
    const push = (text: string) => {
        if (text) out.push({ text, marks: [...marks] });
    };

    for (const token of tokens) {
        switch (token.type) {
            case "text":
                push(token.content);
                cursor += token.content.length;
                break;
            case "text_special":
                // Only `\*` is the editor's own escape; any other stays source.
                push(token.markup === "\\*" ? "*" : token.markup);
                cursor += token.markup.length;
                break;
            case "strong_open":
            case "em_open": {
                const mark: Mark = token.type === "strong_open" ? "bold" : "italic";
                if (token.markup.startsWith("*")) {
                    marks.push(mark);
                    opened.push(mark);
                } else {
                    push(token.markup);
                    opened.push(token.markup);
                }
                cursor += token.markup.length;
                break;
            }
            case "strong_close":
            case "em_close": {
                const what = opened.pop();
                if (what === undefined) return undefined;
                if (what === "bold" || what === "italic") marks.splice(marks.lastIndexOf(what), 1);
                else push(what);
                cursor += token.markup.length;
                break;
            }
            case "code_inline": {
                const end = codeSpanEnd(src, cursor, token.markup.length);
                if (end === undefined) return undefined;
                push(src.slice(cursor, end));
                cursor = end;
                break;
            }
            default:
                return undefined;
        }
    }
    return cursor === src.length ? out : undefined;
}

/** The end of the code span whose opening run of `size` backticks is at `start`. */
function codeSpanEnd(src: string, start: number, size: number): number | undefined {
    let at = start + size;
    while (at < src.length) {
        const next = src.indexOf("`", at);
        if (next < 0) return undefined;
        let run = next;
        while (src[run] === "`") run++;
        if (run - next === size) return run;
        at = run;
    }
    return undefined;
}

/** The stand-ins back to mentions and `@`. */
function expandMarks(pieces: { text: string; marks: Mark[] }[], mentions: { id: string; raw: string }[]): Inline[] {
    const out: Inline[] = [];
    let next = 0;
    for (const piece of pieces) {
        let text = "";
        for (const ch of piece.text) {
            if (ch === MENTION_MARK && next < mentions.length) {
                if (text) out.push({ kind: "text", text, marks: piece.marks });
                text = "";
                const mention = mentions[next++]!;
                out.push({ kind: "mention", id: mention.id, raw: mention.raw, marks: piece.marks });
            } else {
                text += ch === AT_MARK ? "@" : ch;
            }
        }
        if (text) out.push({ kind: "text", text, marks: piece.marks });
    }
    return out;
}

/** A line read without markdown: its mentions, and the rest as plain text. */
function literalLine(line: string, context: ParseContext): Inline[] {
    const out: Inline[] = [];
    let at = 0;
    for (const match of line.matchAll(MENTION_TOKEN)) {
        const end = match.index + match[0].length;
        if (isEscaped(line, match.index)) continue;
        const raw = match[1]!;
        const id = match[2] ?? (line[end] === "(" ? undefined : context.links[raw]);
        if (!id) continue;
        if (match.index > at) out.push({ kind: "text", text: line.slice(at, match.index), marks: [] });
        out.push({ kind: "mention", id, raw, marks: [] });
        at = end;
    }
    if (at < line.length) out.push({ kind: "text", text: line.slice(at), marks: [] });
    return out;
}

const sameMarks = (a: readonly Mark[], b: readonly Mark[]) =>
    a.length === b.length && MARK_ORDER.every((m) => a.includes(m) === b.includes(m));

function mergeText(inline: Inline[]): Inline[] {
    const out: Inline[] = [];
    for (const node of inline) {
        const last = out[out.length - 1];
        if (node.kind === "text" && last?.kind === "text" && sameMarks(last.marks, node.marks)) {
            out[out.length - 1] = { ...last, text: last.text + node.text };
        } else if (node.kind !== "text" || node.text) {
            out.push({ ...node, marks: MARK_ORDER.filter((m) => node.marks.includes(m)) });
        }
    }
    return out;
}

function parseLines(text: string, context: ParseContext): Line[] {
    const lines: Line[] = [];
    // The indents of the open list levels, outermost first.
    let levels: number[] = [];
    for (const line of text.split("\n")) {
        const item = isListLine(line) ? LIST_LINE.exec(line) : null;
        if (!item) {
            levels = [];
            const escaped = ESCAPED_MARKER.exec(line);
            const source = escaped && isListLine(escaped[1]! + escaped[2]!) ? escaped[1]! + escaped[2]! : line;
            lines.push({ kind: "paragraph", inline: parseInline(source, context) });
            continue;
        }
        const indent = item[1]!.length;
        let depth: number;
        if (levels.length === 0) {
            levels = [indent];
            depth = 0;
        } else if (indent >= levels[levels.length - 1]! + INDENT.length) {
            // Past the parent's content: a nested item.
            levels.push(indent);
            depth = levels.length - 1;
        } else {
            while (levels.length > 1 && indent < levels[levels.length - 1]!) levels.pop();
            depth = levels.length - 1;
        }
        lines.push({ kind: "item", depth, inline: parseInline(item[3]!, context) });
    }
    return lines;
}

function inlineJson(inline: Inline[], newIds: ReadonlySet<string>): JSONContent[] {
    return inline.map((node) => {
        const marks = node.marks.length > 0 ? { marks: node.marks.map((type) => ({ type })) } : {};
        if (node.kind === "text") return { type: "text", text: node.text, ...marks };
        const attrs: MentionAttrs = {
            id: node.id,
            label: unescapeMentionText(node.raw),
            raw: node.raw,
            isNew: newIds.has(node.id.toLowerCase()),
        };
        return { type: MENTION_NODE, attrs, ...marks };
    });
}

const paragraph = (content: JSONContent[]): JSONContent =>
    content.length > 0 ? { type: "paragraph", content } : { type: "paragraph" };

/**
 * The composer's text as the editor's document. `links` resolves `@[Name]` mentions;
 * `newEntries` marks the ones made by Create.
 */
export function textToDoc(value: {
    text: string;
    links: Readonly<Record<string, string>>;
    newEntries?: readonly { id: string }[];
}): JSONContent {
    const newIds = new Set((value.newEntries ?? []).map((e) => e.id.toLowerCase()));
    const content: JSONContent[] = [];
    // The open bullet lists, outermost first.
    let lists: JSONContent[] = [];
    for (const line of parseLines(value.text, { links: value.links })) {
        const inline = inlineJson(line.inline, newIds);
        if (line.kind === "paragraph") {
            lists = [];
            content.push(paragraph(inline));
            continue;
        }
        if (lists.length === 0) {
            lists = [{ type: "bulletList", content: [] }];
            content.push(lists[0]!);
        }
        const depth = Math.min(line.depth, lists.length);
        if (depth === lists.length) {
            const parent = lists[depth - 1]!.content!;
            const nested: JSONContent = { type: "bulletList", content: [] };
            parent[parent.length - 1]!.content!.push(nested);
            lists.push(nested);
        } else {
            lists = lists.slice(0, depth + 1);
        }
        lists[depth]!.content!.push({ type: "listItem", content: [paragraph(inline)] });
    }
    return { type: "doc", content };
}

// ── Writing ──────────────────────────────────────────────────────────────────

function inlineOf(node: JSONContent): Inline[] {
    const out: Inline[] = [];
    for (const child of node.content ?? []) {
        const marks = MARK_ORDER.filter((m) => child.marks?.some((mark) => mark.type === m));
        if (child.type === "text" && child.text) {
            out.push({ kind: "text", text: child.text, marks });
        } else if (child.type === MENTION_NODE) {
            const attrs = child.attrs as Partial<MentionAttrs> | undefined;
            const label = attrs?.label ?? "";
            // A mention pasted from elsewhere may have no `raw`: its name, escaped.
            if (attrs?.id) out.push({ kind: "mention", id: attrs.id, raw: attrs.raw || escapeMentionText(label), marks });
            else if (label) out.push({ kind: "text", text: label, marks });
        } else if (child.type === "hardBreak") {
            // Not in the editor's schema; a space keeps the words apart.
            out.push({ kind: "text", text: " ", marks: [] });
        } else if (child.content) {
            out.push(...inlineOf(child));
        }
    }
    return out;
}

function docLines(doc: JSONContent): Line[] {
    const lines: Line[] = [];
    const walk = (node: JSONContent, depth: number) => {
        if (node.type === "bulletList" || node.type === "orderedList") {
            for (const item of node.content ?? []) {
                const [first, ...rest] = item.content ?? [];
                const firstIsText = first && first.type !== "bulletList" && first.type !== "orderedList";
                lines.push({ kind: "item", depth, inline: firstIsText ? inlineOf(first) : [] });
                for (const child of firstIsText ? rest : (item.content ?? [])) {
                    if (child.type === "bulletList" || child.type === "orderedList") walk(child, depth + 1);
                    // A second paragraph in an item: a line indented under it.
                    else
                        lines.push({
                            kind: "paragraph",
                            inline: [{ kind: "text", text: INDENT.repeat(depth + 1), marks: [] }, ...inlineOf(child)],
                        });
                }
            }
            return;
        }
        lines.push({ kind: "paragraph", inline: inlineOf(node) });
    };
    for (const block of doc.content ?? []) walk(block, 0);
    return lines.length > 0 ? lines : [{ kind: "paragraph", inline: [] }];
}

/** Whitespace at a marked node's edges moves outside its markers, as markdown needs. */
function canonical(inline: Inline[]): Inline[] {
    const out: Inline[] = [];
    for (const node of inline) {
        if (node.kind !== "text" || node.marks.length === 0) {
            out.push(node);
            continue;
        }
        const match = /^(\s*)([\s\S]*?)(\s*)$/.exec(node.text)!;
        if (match[1]) out.push({ kind: "text", text: match[1], marks: [] });
        if (match[2]) out.push({ ...node, text: match[2] });
        if (match[3]) out.push({ kind: "text", text: match[3], marks: [] });
    }
    return mergeText(out);
}

/** A literal `*` pairs up unless it stands alone between spaces; `@[` reads as a mention. */
function escapeLiteral(text: string): string {
    return text
        .replace(/@\[/g, "\\@[")
        .replace(/\*/g, (star, at: number) => (/\s/.test(text[at - 1] ?? "") && /\s/.test(text[at + 1] ?? "") ? star : "\\*"));
}

function writeInline(inline: Inline[], mentionMarkup: (node: Extract<Inline, { kind: "mention" }>) => string, escape: boolean) {
    let out = "";
    const open: Mark[] = [];
    for (const node of inline) {
        let keep = 0;
        while (keep < open.length && node.marks.includes(open[keep]!)) keep++;
        while (open.length > keep) out += MARKER[open.pop()!];
        for (const mark of node.marks) {
            if (open.includes(mark)) continue;
            out += MARKER[mark];
            open.push(mark);
        }
        out += node.kind === "text" ? (escape ? escapeLiteral(node.text) : node.text) : mentionMarkup(node);
    }
    while (open.length > 0) out += MARKER[open.pop()!];
    return out;
}

const sameInline = (a: Inline[], b: Inline[]) =>
    a.length === b.length &&
    a.every((node, i) => {
        const other = b[i]!;
        if (node.kind !== other.kind || !sameMarks(node.marks, other.marks)) return false;
        if (node.kind === "text") return node.text === (other as typeof node).text;
        const mention = other as typeof node;
        return node.raw === mention.raw && node.id.toLowerCase() === mention.id.toLowerCase();
    });

/**
 * The editor's document as the composer's text and links. Within one text one display
 * text means one entry (15d): a second entry under the same `@[Name]` is written in the
 * stored form, `@[Name](entry:<id>)`, as `fromStoredText` would leave it.
 */
export function docToText(doc: JSONContent): { text: string; links: Record<string, string> } {
    const lines = docLines(doc).map((line) => ({ ...line, inline: canonical(line.inline) }));

    const links: Record<string, string> = {};
    for (const line of lines) {
        for (const node of line.inline) {
            if (node.kind === "mention" && links[node.raw] === undefined) links[node.raw] = node.id;
        }
    }
    const mentionMarkup = (node: Extract<Inline, { kind: "mention" }>) =>
        links[node.raw]!.toLowerCase() === node.id.toLowerCase() ? `@[${node.raw}]` : `@[${node.raw}](entry:${node.id})`;

    const context: ParseContext = { links };
    const write = (inline: Inline[]) => {
        const plain = writeInline(inline, mentionMarkup, false);
        if (!/[*@\\`_]/.test(plain) || sameInline(parseInline(plain, context), inline)) return plain;
        return writeInline(inline, mentionMarkup, true);
    };

    const text = lines
        .map((line) => {
            const content = write(line.inline);
            if (line.kind === "item") return `${INDENT.repeat(line.depth)}- ${content}`;
            return isListLine(content) ? content.replace(/^( *)/, "$1\\") : content;
        })
        .join("\n");
    return { text, links };
}
