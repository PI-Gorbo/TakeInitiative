// Promote from a selection (15f, design §4): the reader selects text in a rendered
// note, and the API wants a part of the note's stored markdown (`POST …/quotes`
// checks it is a substring once whitespace is collapsed). Pure, so it is unit tested.
//
// `selectionToSource` builds a plain-text view of the source with an offset map. It
// drops what rendering drops: the mention and link syntax (keeping the link text),
// list, quote and heading markers, and code fences. Both sides are then compared
// ignoring whitespace runs and the characters that may or may not be markup (`*`,
// `_`, `` ` ``, `~`, `\`) and the chips' kind icons. The match is mapped back to the
// source and widened to whole mentions and links, so a quote never holds half a chip.
import type { EntrySummary, SessionNote, Visibility } from "./api/types";
import { ENTRY_KINDS, canEditEntry, entriesCalled, type EntryDirectory, type EntryViewer } from "./entries";
import { ENTRY_NAME_MAX, MENTION_SUGGESTIONS_MAX, matchEntries } from "./mentions";

export type SourceRange = { start: number; end: number };

/** A plain-text view of a note's markdown: `map[i]` is the source index of `text[i]`. */
export type PlainView = { text: string; map: number[]; spans: SourceRange[] };

const GUID = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
const BRACKET = String.raw`((?:\\.|[^\\\[\]\n])*)`;
const MENTION = new RegExp(String.raw`^@\[${BRACKET}\]\(entry:${GUID}\)`);
const LINK = new RegExp(String.raw`^\[${BRACKET}\]\(([^()\s]*)\)`);
const FENCE = /^\s{0,3}(```|~~~)/;
// Repeated at a line's start: `> `, a list marker, or a heading's `#`s.
const LINE_MARKER = /^(?:\s{0,3}>\s?|\s*(?:[-*+]|\d{1,9}[.)])\s+|\s{0,3}#{1,6}(?:\s+|$))/;

/** The source as a reader sees it rendered, with each character's source index. */
export function plainView(source: string): PlainView {
    let text = "";
    const map: number[] = [];
    const spans: SourceRange[] = [];
    const emit = (from: number, to: number) => {
        for (let i = from; i < to; i++) {
            text += source[i];
            map.push(i);
        }
    };

    let at = 0;
    while (at < source.length) {
        const lineEnd = source.indexOf("\n", at) === -1 ? source.length : source.indexOf("\n", at);
        const line = source.slice(at, lineEnd);
        let i = at;
        if (FENCE.test(line)) {
            i = lineEnd;
        } else {
            for (let m = LINE_MARKER.exec(source.slice(i, lineEnd)); m && m[0].length > 0;) {
                i += m[0].length;
                m = LINE_MARKER.exec(source.slice(i, lineEnd));
            }
        }
        while (i < lineEnd) {
            const rest = source.slice(i, lineEnd);
            const link = source[i] === "@" ? MENTION.exec(rest) : source[i] === "[" ? LINK.exec(rest) : null;
            if (link) {
                const textStart = i + (source[i] === "@" ? 2 : 1);
                emit(textStart, textStart + link[1]!.length);
                spans.push({ start: i, end: i + link[0].length });
                i += link[0].length;
                continue;
            }
            if (source[i] === "\\" && i + 1 < lineEnd) {
                emit(i + 1, i + 2);
                i += 2;
                continue;
            }
            emit(i, i + 1);
            i++;
        }
        if (lineEnd < source.length) emit(lineEnd, lineEnd + 1);
        at = lineEnd + 1;
    }
    return { text, map, spans };
}

// Characters that may be markup on one side and text on the other, and the chips'
// icons (drawn in the page, absent from the source).
const IGNORED = new Set(["*", "_", "`", "~", "\\", "️", "‍", "​"]);
const ICONS = new Set(ENTRY_KINDS.map((k) => String.fromCodePoint(k.icon.codePointAt(0)!)));

/** `text` with whitespace runs as one space and the ignored characters dropped. */
export function normalizeForMatch(text: string): {
    text: string;
    map: number[];
} {
    let out = "";
    const map: number[] = [];
    let space = false;
    let i = 0;
    for (const ch of text) {
        const at = i;
        i += ch.length;
        if (IGNORED.has(ch) || ICONS.has(ch)) continue;
        if (/\s/.test(ch)) {
            space = out.length > 0;
            continue;
        }
        if (space) {
            out += " ";
            map.push(at);
            space = false;
        }
        out += ch;
        for (let k = 0; k < ch.length; k++) map.push(at);
    }
    return { text: out, map };
}

/**
 * The part of a note's source that a selection of its rendered text shows, widened to
 * whole mentions and links. Null when the selection is not text of this note (it
 * crossed into something else, or is only whitespace and icons). The first match
 * wins when the text occurs more than once.
 */
export function selectionToSource(source: string, selected: string): (SourceRange & { text: string }) | null {
    const view = plainView(source);
    const plain = normalizeForMatch(view.text);
    const wanted = normalizeForMatch(selected).text.trim();
    if (wanted === "") return null;
    const found = plain.text.indexOf(wanted);
    if (found === -1) return null;

    const firstPlain = plain.map[found]!;
    const lastPlain = plain.map[found + wanted.length - 1]!;
    let start = view.map[firstPlain]!;
    const lastSource = view.map[lastPlain]!;
    // A surrogate pair is two source characters.
    let end = lastSource + (source.codePointAt(lastSource)! > 0xffff ? 2 : 1);
    for (const span of view.spans) {
        if (start > span.start && start < span.end) start = span.start;
        if (end > span.start && end < span.end) end = span.end;
    }
    return { start, end, text: source.slice(start, end) };
}

/** What the server compares: whitespace runs collapsed, trimmed. */
export const collapseWhitespace = (text: string) => text.replace(/\s+/g, " ").trim();

/**
 * Whether a quote text is still a part of the note (the server's 400 otherwise), so
 * the dialog can say so before sending.
 */
export const isExcerptOf = (noteText: string, excerpt: string) =>
    collapseWhitespace(excerpt) !== "" && collapseWhitespace(noteText).includes(collapseWhitespace(excerpt));

// ── Picking the entry (`EntryPicker`) ────────────────────────────────────────

export type PromoteTarget = { kind: "entry"; entry: EntrySummary; alias?: string } | { kind: "create"; name: string };

/**
 * The entries a note can be promoted into: 15d's matching, over the entries the viewer
 * can edit (the API's rule for promote). Create "…" comes last when no entry the
 * viewer can see is called that (the API's duplicate-name 409 otherwise).
 */
export function promoteTargets(
    query: string,
    directory: EntryDirectory,
    viewer: EntryViewer,
    limit = MENTION_SUGGESTIONS_MAX
): PromoteTarget[] {
    const editable = directory.items.filter(({ entry }) => canEditEntry(entry, viewer));
    const targets: PromoteTarget[] = matchEntries(query, { byId: directory.byId, items: editable }, limit).map((m) => ({
        kind: "entry",
        entry: m.entry,
        alias: m.alias,
    }));
    const name = query.trim();
    if (name !== "" && name.length <= ENTRY_NAME_MAX && entriesCalled(directory, name).length === 0) {
        targets.push({ kind: "create", name });
    }
    return targets;
}

/**
 * Who reads a note, and so its quote (15e): a hidden `Everyone` note is read by the
 * DMs and its author only. An entry created from the dialog gets this visibility.
 */
export const noteAudience = (note: Pick<SessionNote, "visibility" | "isHidden">): Visibility =>
    note.isHidden && note.visibility === "Everyone" ? "DM" : note.visibility;
