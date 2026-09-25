// `@` mentions while writing (15d, design §3 and §3a). Pure, so it is unit tested
// without Nuxt; `useMentionPicker` wires it to a textarea, and the composer, the note
// editor and (15f) the article editor share both.
//
// The text box is a plain `<textarea>` (14d), where a GUID would be unreadable. So a
// mention reads `@[Gundren Rockseeker]` there, and the entry id lives beside the text
// in `links`, keyed by the text between the brackets. `toStoredText` writes the stored
// form `@[Gundren Rockseeker](entry:<id>)` when the note is posted, and
// `fromStoredText` reads it back for editing. Within one text, one display text means
// one entry: a second entry under the same text stays in the stored form.
import type { EntryKind, EntrySummary, Visibility } from "./api/types";
import { canChangeEntryAccess, resolveEntry, type EntryDirectory, type EntryViewer } from "./entries";
import { mentionedEntryIds } from "./markdown";

// ── The composer's mention text ──────────────────────────────────────────────

/** A new entry made by Create, sent as `newEntries[]` with the note (15b). */
export type NewEntry = { id: string; name: string; kind: EntryKind };

/**
 * The text box's text, plus what its `@[…]` mentions link to. `links` is keyed by the
 * raw text between the brackets (escapes included), and maps to an entry id.
 */
export type MentionText = {
    text: string;
    links: Record<string, string>;
    newEntries: NewEntry[];
};

export const emptyMentionText = (text = ""): MentionText => ({ text, links: {}, newEntries: [] });

// Characters that would change how a name reads as markdown (the same set as
// `mentionMarkup` in `utils/entries.ts`, so both forms carry the same bracket text).
const MARKDOWN_SPECIALS = /[\\`*_[\]<>~&]/g;
const ESCAPED = /\\([!-/:-@[-`{-~])/g;

/** A name as bracket text: escaped so the chip reads back exactly the name. */
export const escapeMentionText = (name: string) => name.replace(MARKDOWN_SPECIALS, (c) => `\\${c}`);
/** Bracket text as plain text: backslash escapes dropped. */
export const unescapeMentionText = (raw: string) => raw.replace(ESCAPED, "$1");

const GUID = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
const BRACKET_TEXT = String.raw`(?:\\.|[^\\\[\]\n])*`;
/** `@[text]` not followed by `(`: a mention in the composer form. */
const DISPLAY_TOKEN = new RegExp(String.raw`@\[(${BRACKET_TEXT})\](?!\()`, "g");
/** `@[text](entry:<id>)`: a mention in the stored form. */
const STORED_TOKEN = new RegExp(String.raw`@\[(${BRACKET_TEXT})\]\(entry:(${GUID})\)`, "g");

/** True when the character at `index` is escaped by an odd run of backslashes. */
function isEscaped(text: string, index: number): boolean {
    let slashes = 0;
    for (let i = index - 1; i >= 0 && text[i] === "\\"; i--) slashes++;
    return slashes % 2 === 1;
}

/** Every unescaped `@[…]` in the composer form, with its range and bracket text. */
export function displayTokens(text: string): { start: number; end: number; raw: string }[] {
    const tokens: { start: number; end: number; raw: string }[] = [];
    for (const match of text.matchAll(DISPLAY_TOKEN)) {
        if (isEscaped(text, match.index)) continue;
        tokens.push({ start: match.index, end: match.index + match[0].length, raw: match[1]! });
    }
    return tokens;
}

/** The composer form to the stored form: each linked `@[t]` becomes `@[t](entry:<id>)`. */
export function toStoredText(text: string, links: Readonly<Record<string, string>>): string {
    let out = "";
    let at = 0;
    for (const token of displayTokens(text)) {
        const id = links[token.raw];
        if (!id) continue;
        out += text.slice(at, token.end) + `(entry:${id})`;
        at = token.end;
    }
    return out + text.slice(at);
}

/**
 * The stored form to the composer form, for editing a note. The first entry under a
 * display text takes it; a different entry under the same text stays in the stored
 * form, so nothing is lost.
 */
export function fromStoredText(stored: string): { text: string; links: Record<string, string> } {
    const links: Record<string, string> = {};
    let text = "";
    let at = 0;
    for (const match of stored.matchAll(STORED_TOKEN)) {
        if (isEscaped(stored, match.index)) continue;
        const raw = match[1]!;
        const id = match[2]!;
        const linked = links[raw];
        if (linked !== undefined && linked.toLowerCase() !== id.toLowerCase()) continue;
        links[raw] = linked ?? id;
        text += stored.slice(at, match.index) + `@[${raw}]`;
        at = match.index + match[0].length;
    }
    return { text: text + stored.slice(at), links };
}

/**
 * What a post or an edit sends: the stored text, and the new entries it still
 * mentions. A Create whose mention was deleted is dropped (15d), using the same parser
 * the API uses, so the API never sees an unmentioned id (its 400).
 */
export function mentionBody(state: MentionText, text: string = state.text): { text: string; newEntries: NewEntry[] } {
    const stored = toStoredText(text, state.links);
    const mentioned = new Set(mentionedEntryIds(stored));
    return {
        text: stored,
        newEntries: state.newEntries.filter((e) => mentioned.has(e.id.toLowerCase())),
    };
}

/**
 * The composer's links for a text with a stored-form mention of an entry at the start:
 * `@[Name] ` plus a link, or the stored form itself when that display text already
 * links another entry.
 */
export function linkOrMarkup(
    links: Readonly<Record<string, string>>,
    entry: Pick<EntrySummary, "id" | "name">
): { markup: string; links: Record<string, string> } {
    const raw = escapeMentionText(entry.name);
    const linked = links[raw];
    if (linked !== undefined && linked.toLowerCase() !== entry.id.toLowerCase()) {
        return { markup: `@[${raw}](entry:${entry.id})`, links: { ...links } };
    }
    return { markup: `@[${raw}]`, links: { ...links, [raw]: entry.id } };
}

// ── "Add a note about X" (15c) ───────────────────────────────────────────────

/**
 * The composer's start for a note about an entry: the text begins with its mention,
 * as `@[Name]` plus a link, and a `DM` or `Me` entry makes the note match, so the
 * reveal warning does not fire on the note's own subject.
 */
export function aboutPrefill(
    current: Pick<MentionText, "text" | "links">,
    entry: Pick<EntrySummary, "id" | "name" | "visibility">
): { text: string; links: Record<string, string>; visibility: Visibility | undefined } {
    const { markup, links } = linkOrMarkup(current.links, entry);
    const rest = current.text.trimStart();
    return {
        text: rest.startsWith(markup) ? current.text : `${markup} ${rest}`,
        links,
        visibility: entry.visibility === "Everyone" ? undefined : entry.visibility,
    };
}

// ── The active query ─────────────────────────────────────────────────────────

/** The longest `@` query, in characters after the `@`. */
export const MENTION_QUERY_MAX = 40;

/**
 * The `@` query at the caret:
 * - `typed`: an `@` at the start or after whitespace or `(`, then up to 40 characters
 *   with no newline, up to the caret. Picking replaces `start`..`end`.
 * - `bracket`: the caret is inside an unlinked `@[…]`. The bracket text is the query,
 *   and picking links it without changing the text (the display-text override).
 */
export type ActiveMention =
    | { kind: "typed"; start: number; end: number; query: string }
    | { kind: "bracket"; start: number; end: number; query: string; raw: string };

export function activeMention(
    text: string,
    caret: number,
    links: Readonly<Record<string, string>>
): ActiveMention | null {
    // Inside `@[…]`: between the brackets, inclusive of the closing one's left side.
    for (const token of displayTokens(text)) {
        if (caret < token.start + 2 || caret > token.end - 1) continue;
        if (links[token.raw] !== undefined) return null;
        return { kind: "bracket", start: token.start, end: token.end, query: unescapeMentionText(token.raw), raw: token.raw };
    }

    const lineStart = text.lastIndexOf("\n", caret - 1) + 1;
    const at = text.lastIndexOf("@", caret - 1);
    if (at < lineStart || caret - at - 1 > MENTION_QUERY_MAX) return null;
    const before = at === 0 ? "" : text[at - 1]!;
    if (before !== "" && !/[\s(]/.test(before)) return null;

    let query = text.slice(at + 1, caret);
    // `@[Gund` with no closing bracket yet: the query is what follows the bracket.
    if (query.startsWith("[")) {
        if (query.includes("]")) return null;
        query = query.slice(1);
    }
    if (/^\s/.test(query)) return null;
    return { kind: "typed", start: at, end: caret, query };
}

// ── Matching ─────────────────────────────────────────────────────────────────

/** The API's limit on an entry's name (15a). */
export const ENTRY_NAME_MAX = 100;

/** At most this many entries are suggested. */
export const MENTION_SUGGESTIONS_MAX = 8;

/** Lower case, trimmed, accents folded: "Gündren " → "gundren". */
export const foldForMatch = (value: string) =>
    value
        .normalize("NFD")
        .replace(/\p{M}/gu, "")
        .toLocaleLowerCase()
        .replace(/\s+/g, " ")
        .trim();

/** 0 exact, 1 prefix, 2 word prefix, 3 substring; undefined for no match. */
export function matchRank(candidate: string, foldedQuery: string): number | undefined {
    const folded = foldForMatch(candidate);
    if (!foldedQuery) return 3;
    if (folded === foldedQuery) return 0;
    if (folded.startsWith(foldedQuery)) return 1;
    const index = folded.indexOf(foldedQuery);
    if (index < 0) return undefined;
    return /[\s\-'"(]/.test(folded[index - 1]!) ? 2 : 3;
}

export type EntryMatch = {
    entry: EntrySummary;
    mentionCount: number;
    /** Set when an alias matched better than the name: "Gundren Rockseeker · aka Rockseeker". */
    alias?: string;
    rank: number;
};

/**
 * Entries whose name or an alias matches the query, ranked exact, then prefix, then
 * word prefix, then substring, with the most mentioned first among equals, then A–Z.
 * An empty query lists the most mentioned. At most `limit` results.
 */
export function matchEntries(query: string, directory: EntryDirectory, limit = MENTION_SUGGESTIONS_MAX): EntryMatch[] {
    const wanted = foldForMatch(query);
    const matches: EntryMatch[] = [];
    for (const { entry, mentionCount } of directory.items) {
        let rank = matchRank(entry.name, wanted);
        let alias: string | undefined;
        for (const candidate of entry.aliases) {
            const aliasRank = matchRank(candidate, wanted);
            if (aliasRank !== undefined && (rank === undefined || aliasRank < rank)) {
                rank = aliasRank;
                alias = candidate;
            }
        }
        if (rank !== undefined) matches.push({ entry, mentionCount, alias, rank });
    }
    return matches
        .sort(
            (a, b) =>
                a.rank - b.rank ||
                b.mentionCount - a.mentionCount ||
                a.entry.name.localeCompare(b.entry.name, undefined, { sensitivity: "base" })
        )
        .slice(0, limit);
}

export type MentionSuggestion =
    | {
          kind: "entry";
          entryId: string;
          name: string;
          entryKind: EntryKind;
          alias?: string;
          /** Made by Create in this text and not posted yet. */
          isNew: boolean;
      }
    | { kind: "create"; name: string };

/**
 * The suggestions for a query: this text's new entries that match, then the
 * directory's matches (8 at most together), then **Create "…"** when no name or alias
 * equals the query. A query that ends with a space and matches nothing is closed (an
 * empty list), so writing on past an unknown `@word` puts nothing in the way.
 * `preferIds` go first: in a bracket query, the entries this text linked before the
 * bracket text was edited, so "pick, then override the text, then pick again" works.
 */
export function mentionSuggestions(
    query: string,
    directory: EntryDirectory,
    newEntries: readonly NewEntry[] = [],
    preferIds: readonly string[] = []
): MentionSuggestion[] {
    const wanted = foldForMatch(query);
    const out: MentionSuggestion[] = [];
    const seen = new Set<string>();
    const add = (s: Extract<MentionSuggestion, { kind: "entry" }>) => {
        const key = s.entryId.toLowerCase();
        if (seen.has(key) || out.length >= MENTION_SUGGESTIONS_MAX) return;
        seen.add(key);
        out.push(s);
    };

    for (const id of preferIds) {
        const pending = newEntries.find((e) => e.id.toLowerCase() === id.toLowerCase());
        if (pending) {
            add({ kind: "entry", entryId: pending.id, name: pending.name, entryKind: pending.kind, isNew: true });
            continue;
        }
        const entry = resolveEntry(directory, id);
        if (entry) add({ kind: "entry", entryId: entry.id, name: entry.name, entryKind: entry.kind, isNew: false });
    }
    const pendingMatches = newEntries
        .map((e) => ({ e, rank: matchRank(e.name, wanted) }))
        .filter((m) => m.rank !== undefined)
        .sort((a, b) => a.rank! - b.rank!);
    for (const { e } of pendingMatches) {
        add({ kind: "entry", entryId: e.id, name: e.name, entryKind: e.kind, isNew: true });
    }
    const directoryMatches = matchEntries(query, directory);
    for (const match of directoryMatches) {
        add({
            kind: "entry",
            entryId: match.entry.id,
            name: match.entry.name,
            entryKind: match.entry.kind,
            alias: match.alias,
            isNew: false,
        });
    }

    if (/\s$/.test(query) && pendingMatches.length === 0 && directoryMatches.length === 0) return [];

    const name = query.trim();
    const exact = (candidate: string) => foldForMatch(candidate) === wanted;
    const taken =
        directory.items.some(({ entry }) => exact(entry.name) || entry.aliases.some(exact)) ||
        newEntries.some((e) => exact(e.name));
    if (name && name.length <= ENTRY_NAME_MAX && !taken) out.push({ kind: "create", name });
    return out;
}

// ── Picking ──────────────────────────────────────────────────────────────────

export type MentionEdit = MentionText & { caret: number };

/**
 * Links the active query to an entry. A typed query is replaced by `@[Name] ` (a
 * space is added unless one follows); a bracket query keeps its text and gains the
 * link. When the display text already links another entry, the stored form is
 * written instead, so one display text still means one entry.
 */
export function pickEntry(
    state: MentionText,
    active: ActiveMention,
    entry: { id: string; name: string }
): MentionEdit {
    if (active.kind === "bracket") {
        return { ...state, links: { ...state.links, [active.raw]: entry.id }, caret: active.end };
    }
    const { markup, links } = linkOrMarkup(state.links, entry);
    const after = state.text.slice(active.end);
    const spacer = /^\s/.test(after) ? "" : " ";
    const text = state.text.slice(0, active.start) + markup + spacer + after;
    return {
        text,
        links,
        newEntries: state.newEntries,
        caret: active.start + markup.length + spacer.length,
    };
}

/**
 * **Create "…"**: a new entry with a client-made id, linked like a pick. It is
 * created with the note when the note is posted, and gets the note's visibility then.
 */
export function createEntry(
    state: MentionText,
    active: ActiveMention,
    kind: EntryKind,
    newId: string
): MentionEdit {
    const name = active.query.trim();
    const newEntries = [...state.newEntries, { id: newId, name, kind }];
    return pickEntry({ ...state, newEntries }, active, { id: newId, name });
}

/**
 * The `@` toolbar button: inserts `@` at the caret (with a space before it unless the
 * caret is at the start of a line or after a space or `(`). A selection becomes the
 * query.
 */
export function insertMentionTrigger(edit: { text: string; selectionStart: number; selectionEnd: number }) {
    const { text, selectionStart: start, selectionEnd: end } = edit;
    const before = text.slice(0, start);
    const spacer = before === "" || /[\s(]$/.test(before) ? "" : " ";
    const inserted = `${spacer}@`;
    const caret = end + inserted.length;
    return { text: before + inserted + text.slice(start), selectionStart: caret, selectionEnd: caret };
}

/** Entries linked in `links` whose display text is no longer in the text. */
export function orphanedLinkIds(state: Pick<MentionText, "text" | "links">): string[] {
    const present = new Set(displayTokens(state.text).map((t) => t.raw));
    return Object.entries(state.links)
        .filter(([raw]) => !present.has(raw))
        .map(([, id]) => id);
}

/** Links and new entries still used by the text, so a draft does not grow forever. */
export function pruneMentionText(state: MentionText): MentionText {
    const present = new Set(displayTokens(state.text).map((t) => t.raw));
    const links = Object.fromEntries(Object.entries(state.links).filter(([raw]) => present.has(raw)));
    const stored = toStoredText(state.text, links);
    const ids = new Set(mentionedEntryIds(stored));
    return { text: state.text, links, newEntries: state.newEntries.filter((e) => ids.has(e.id.toLowerCase())) };
}

// ── The linked entries (the composer's "Links" row) ──────────────────────────

export type LinkedMention = { entryId: string; name: string; entryKind: EntryKind; isNew: boolean };

/** The entries the text mentions, in order, new ones marked. Unknown ids are left out. */
export function linkedMentions(state: MentionText, directory: EntryDirectory): LinkedMention[] {
    const out: LinkedMention[] = [];
    for (const id of mentionedEntryIds(toStoredText(state.text, state.links))) {
        const pending = state.newEntries.find((e) => e.id.toLowerCase() === id);
        if (pending) {
            out.push({ entryId: pending.id, name: pending.name, entryKind: pending.kind, isNew: true });
            continue;
        }
        const entry = resolveEntry(directory, id);
        if (entry) out.push({ entryId: entry.id, name: entry.name, entryKind: entry.kind, isNew: false });
    }
    return out;
}

// ── The reveal warning ───────────────────────────────────────────────────────

const REACH: Record<Visibility, number> = { Everyone: 0, DM: 1, Me: 2 };

export type RevealItem = { entry: EntrySummary; canReveal: boolean };

/**
 * The linked entries whose audience does not cover the note's (§3, invariant 5): a
 * `DM` entry in an `Everyone` note, or a `Me` entry in an `Everyone` or `DM` note.
 * An entry the author can see with the same visibility as the note always covers it:
 * a `DM` entry is the DMs plus its creator, and the author can see it only as a DM or
 * as its creator. `canReveal` is whether the viewer may change its visibility.
 */
export function revealCheck(
    noteVisibility: Visibility,
    storedText: string,
    directory: EntryDirectory,
    viewer: EntryViewer
): RevealItem[] {
    const out: RevealItem[] = [];
    for (const id of mentionedEntryIds(storedText)) {
        const entry = resolveEntry(directory, id);
        if (!entry || REACH[entry.visibility] <= REACH[noteVisibility]) continue;
        out.push({ entry, canReveal: canChangeEntryAccess(entry, viewer) });
    }
    return out;
}

/** "Gundren is hidden from players." / "Gundren and Glasstaff are hidden from players." */
export function revealMessage(items: readonly RevealItem[], noteVisibility: Visibility): string {
    const names = items.map((i) => i.entry.name);
    const list =
        names.length <= 1 ? (names[0] ?? "") : `${names.slice(0, -1).join(", ")} and ${names[names.length - 1]}`;
    const verb = names.length === 1 ? "is" : "are";
    const from = noteVisibility === "Everyone" ? "players" : "the DMs";
    return `${list} ${verb} hidden from ${from}.`;
}

// ── Errors from `newEntries` (15b) ───────────────────────────────────────────

export type NewEntryError =
    /** 409: an id already has a stream. A retry after a timeout: the note was posted. */
    | { kind: "alreadyCreated"; entryId: string }
    /** 409: a new entry's name is taken by a visible entry. */
    | { kind: "duplicate"; newEntryId: string; existingEntryId: string }
    /** 400: an id that the text does not mention. */
    | { kind: "unmentioned"; message: string };

export function newEntryErrorFrom(error: unknown): NewEntryError | null {
    const response = (error as { response?: { status?: number; data?: { errors?: Record<string, unknown> } } } | undefined)
        ?.response;
    const errors = response?.data?.errors;
    const first = (key: string) => {
        const value = errors?.[key];
        return Array.isArray(value) && typeof value[0] === "string" ? (value[0] as string) : undefined;
    };
    if (response?.status === 409) {
        const already = first("alreadyCreatedEntryId");
        if (already) return { kind: "alreadyCreated", entryId: already };
        const existing = first("existingEntryId");
        const created = first("newEntryId");
        if (existing && created) return { kind: "duplicate", newEntryId: created, existingEntryId: existing };
    }
    if (response?.status === 400) {
        const message = first("newEntries");
        if (message) return { kind: "unmentioned", message };
    }
    return null;
}

/**
 * After a duplicate-name 409: the new entry is dropped, and its mentions link the
 * entry that already has the name.
 */
export function relinkNewEntry(state: MentionText, newEntryId: string, existingEntryId: string): MentionText {
    const same = (id: string) => id.toLowerCase() === newEntryId.toLowerCase();
    return {
        text: state.text,
        links: Object.fromEntries(Object.entries(state.links).map(([raw, id]) => [raw, same(id) ? existingEntryId : id])),
        newEntries: state.newEntries.filter((e) => !same(e.id)),
    };
}

// ── Drafts (14d) ─────────────────────────────────────────────────────────────

/** A draft as saved: JSON `{ text, links, newEntries }`. */
export const serializeDraft = (draft: MentionText) => JSON.stringify(pruneMentionText(draft));

/** A saved draft. A 14d draft (a plain string) still loads, as text with no links. */
export function parseDraft(saved: string | null | undefined): MentionText {
    if (!saved) return emptyMentionText();
    if (saved.startsWith("{")) {
        try {
            const value = JSON.parse(saved) as Partial<MentionText>;
            if (typeof value.text === "string") {
                const links =
                    value.links && typeof value.links === "object"
                        ? Object.fromEntries(
                              Object.entries(value.links).filter(
                                  (pair): pair is [string, string] => typeof pair[1] === "string"
                              )
                          )
                        : {};
                const newEntries = Array.isArray(value.newEntries)
                    ? value.newEntries.filter(
                          (e): e is NewEntry =>
                              !!e && typeof e.id === "string" && typeof e.name === "string" && typeof e.kind === "string"
                      )
                    : [];
                return { text: value.text, links, newEntries };
            }
        } catch {
            // Not JSON: a 14d draft that happens to start with "{".
        }
    }
    return emptyMentionText(saved);
}

// ── New entries before the server has them ───────────────────────────────────

/**
 * A new entry as the directory shows it between the post and its `entryUpserted`, so
 * the optimistic note's chip shows at once. It has the note's visibility, as the API
 * gives it (15b). The push, or the next read of the list, replaces it.
 */
export function pendingEntrySummary(
    entry: NewEntry,
    args: { creatorMemberId: string; visibility: Visibility; now: Date }
): EntrySummary {
    const at = args.now.toISOString();
    return {
        id: entry.id,
        name: entry.name,
        kind: entry.kind,
        aliases: [],
        visibility: args.visibility,
        editAccess: "Anyone",
        creatorMemberId: args.creatorMemberId,
        createdAt: at,
        updatedAt: at,
    };
}
