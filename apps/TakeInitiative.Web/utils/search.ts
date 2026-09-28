// ⌘K search's pure rules (17b): reading the input, flattening the server's sections
// into rows, the keyboard cursor, snippet highlights, where a hit goes, and the
// recent entries kept in `localStorage`. `SearchSheet` only wires these to the DOM.
import type {
    CombatCard,
    EntryListItem,
    SearchHit,
    SearchResponse,
    SearchSection,
    SearchSectionKey,
    Snippet,
    Visibility,
} from "./api/types";
import type { EntryDirectory } from "./entries";
import { NOTE_LINK_PARAM } from "./noteActions";

// ── Input ────────────────────────────────────────────────────────────────────

/** `all` searches every section, `@` Entries only, and `>` actions only (17c, no request). */
export type SearchScope = "all" | "entries" | "actions";
export type SearchInput = { scope: SearchScope; text: string };

/** The server takes 1–100 characters after the prefix. */
export const SEARCH_TEXT_MAX = 100;

export function parseSearchInput(raw: string): SearchInput {
    const trimmed = raw.trim();
    const prefix = trimmed[0];
    if (prefix === "@") return { scope: "entries", text: trimmed.slice(1).trim().slice(0, SEARCH_TEXT_MAX) };
    if (prefix === ">") return { scope: "actions", text: trimmed.slice(1).trim().slice(0, SEARCH_TEXT_MAX) };
    return { scope: "all", text: trimmed.slice(0, SEARCH_TEXT_MAX) };
}

/** `GET search`'s query string for an input, or null when nothing is sent. */
export function searchParams(input: SearchInput): { q: string; sections?: string } | null {
    if (!input.text || input.scope === "actions") return null;
    return input.scope === "entries" ? { q: input.text, sections: "entries" } : { q: input.text };
}

// ── Rows ─────────────────────────────────────────────────────────────────────

export const SECTION_LABELS: Record<SearchSectionKey, { header: string; more: string }> = {
    Entries: { header: "Entries", more: "Show more entries" },
    Notes: { header: "Notes", more: "Show more notes" },
    Images: { header: "Images", more: "Show more images" },
    Sessions: { header: "Sessions", more: "Show more sessions" },
    Combats: { header: "Combats", more: "Show more combats" },
    Reference: { header: "Reference", more: "Show more reference" },
};

/**
 * Sections the API sends that the sheet cannot draw yet. The API answers Reference from 20b; its
 * rows come in 20c, which empties this.
 */
export const HIDDEN_SECTIONS: ReadonlySet<SearchSectionKey> = new Set<SearchSectionKey>(["Reference"]);

/** The most a section shows after "Show more": the server's largest `take`. */
export const SEARCH_MORE_TAKE = 20;

/** The server's sections, then the web's own Actions (17c). */
export type SearchRowSection = SearchSectionKey | "Actions";

export type SearchRow =
    | { type: "header"; id: string; section: SearchRowSection; label: string }
    | { type: "hit"; id: string; section: SearchSectionKey; hit: SearchHit }
    | { type: "more"; id: string; section: SearchSectionKey; label: string }
    | { type: "action"; id: string; section: "Actions"; actionId: string; icon: string; label: string };

/** The Actions section's rows (17c), after every other section. */
export function actionRows(actions: readonly { id: string; icon: string; label: string }[]): SearchRow[] {
    if (actions.length === 0) return [];
    return [
        { type: "header", id: "header-Actions", section: "Actions", label: "Actions" },
        ...actions.map(
            (a): SearchRow => ({
                type: "action",
                id: `action-${a.id}`,
                section: "Actions",
                actionId: a.id,
                icon: a.icon,
                label: a.label,
            })
        ),
    ];
}

/** A hit's stable id, for keys and `aria-activedescendant`. */
export function hitKey(hit: SearchHit): string {
    if (hit.entry) return `entry-${hit.entry.entry.id}`;
    if (hit.note) return `note-${hit.note.id}`;
    if (hit.session) return `session-${hit.session.session.id}`;
    if (hit.combat) return `combat-${hit.combat.combat.id}`;
    if (hit.reference) return `reference-${hit.reference.provider}-${hit.reference.id}`;
    return "hit";
}

/**
 * Sections, in the server's order, as rows under their headers. A section with more
 * hits ends in "Show more …", unless it has been shown in full already.
 */
export function searchRows(
    response: Pick<SearchResponse, "sections"> | undefined,
    expanded: ReadonlySet<SearchSectionKey> = new Set()
): SearchRow[] {
    const rows: SearchRow[] = [];
    for (const section of response?.sections ?? []) {
        if (section.hits.length === 0 || HIDDEN_SECTIONS.has(section.key)) continue;
        const key = section.key;
        rows.push({ type: "header", id: `header-${key}`, section: key, label: SECTION_LABELS[key].header });
        for (const hit of section.hits) {
            rows.push({ type: "hit", id: `${key}-${hitKey(hit)}`, section: key, hit });
        }
        if (section.hasMore && !expanded.has(key)) {
            rows.push({ type: "more", id: `more-${key}`, section: key, label: SECTION_LABELS[key].more });
        }
    }
    return rows;
}

/** The empty input's rows: the recent entries under their own header, as entry hits. */
export function recentRows(items: readonly EntryListItem[]): SearchRow[] {
    if (items.length === 0) return [];
    return [
        { type: "header", id: "header-recent", section: "Entries", label: "Recent entries" },
        ...items.map(
            (item): SearchRow => ({
                type: "hit",
                id: `recent-entry-${item.entry.id}`,
                section: "Entries",
                hit: { kind: "Entry", entry: { entry: item.entry, mentionCount: item.mentionCount, matchedOn: "Name" } },
            })
        ),
    ];
}

/** The response with one section replaced by its longer version ("Show more"). */
export function replaceSection<R extends Pick<SearchResponse, "sections">>(response: R, section: SearchSection): R {
    return {
        ...response,
        sections: response.sections.map((s) => (s.key === section.key ? section : s)),
    };
}

/** Whether a response found nothing at all. */
export const isEmptyResponse = (response: Pick<SearchResponse, "sections">) =>
    response.sections.every((s) => s.hits.length === 0 || HIDDEN_SECTIONS.has(s.key));

// ── The cursor ───────────────────────────────────────────────────────────────

const selectable = (row: { type: string }) => row.type !== "header";

/** The first row a cursor can rest on, or -1. */
export const firstRow = (rows: readonly { type: string }[]) => rows.findIndex(selectable);

/**
 * ↑ / ↓: the next row by `delta`, skipping headers and wrapping at the ends. -1 when
 * nothing can be chosen; from -1, ↓ goes to the first row and ↑ to the last.
 */
export function moveCursor(rows: readonly { type: string }[], current: number, delta: 1 | -1): number {
    const count = rows.length;
    if (!rows.some(selectable)) return -1;
    let index = current < 0 || current >= count ? (delta > 0 ? -1 : count) : current;
    for (let step = 0; step < count; step++) {
        index = (index + delta + count) % count;
        if (selectable(rows[index])) return index;
    }
    return -1;
}

// ── Snippets ─────────────────────────────────────────────────────────────────

export type SnippetSegment = { text: string; mark: boolean };

/**
 * A snippet as text runs, each highlighted or not, for `<mark>` around text nodes.
 * Ranges are clamped to the text, sorted, and merged where they touch or overlap.
 */
export function snippetSegments(snippet: Pick<Snippet, "text" | "highlights">): SnippetSegment[] {
    const { text } = snippet;
    const ranges = snippet.highlights
        .map((h) => [Math.max(0, h.start), Math.min(text.length, h.start + h.length)] as const)
        .filter(([start, end]) => end > start)
        .sort((a, b) => a[0] - b[0]);
    const merged: [number, number][] = [];
    for (const [start, end] of ranges) {
        const last = merged[merged.length - 1];
        if (last && start <= last[1]) last[1] = Math.max(last[1], end);
        else merged.push([start, end]);
    }
    const segments: SnippetSegment[] = [];
    let at = 0;
    for (const [start, end] of merged) {
        if (start > at) segments.push({ text: text.slice(at, start), mark: false });
        segments.push({ text: text.slice(start, end), mark: true });
        at = end;
    }
    if (at < text.length) segments.push({ text: text.slice(at), mark: false });
    return segments;
}

// ── Row text ─────────────────────────────────────────────────────────────────

/** "🔒 DM" / "🔒 Me", or null for `Everyone`. */
export const lockLabel = (visibility: Visibility): string | null =>
    visibility === "Everyone" ? null : `🔒 ${visibility}`;

/** A session's line: "Session 12 · Sat 20 Sep · The Triboar Trail". */
export const sessionLine = (number: number, date: string, title?: string | null) =>
    [`Session ${number}`, date, title?.trim()].filter(Boolean).join(" · ");

/**
 * A combat's line (18f): "Live · Round 3" while it runs, else its session and status,
 * "S12 · Finished" or "S12 · Draft".
 */
export function combatHitLine(card: Pick<CombatCard, "status" | "round">, sessionNumber: number): string {
    if (card.status === "Active") return `Live · Round ${card.round}`;
    return `S${sessionNumber} · ${card.status}`;
}

// ── Where a hit goes ─────────────────────────────────────────────────────────

export const SESSION_LINK_PARAM = "session";
export const BLOCK_LINK_PARAM = "block";

export type SearchTarget = { path: string; query: Record<string, string> };

/**
 * An entry opens its page (at the matching block for an article hit); a note or an
 * image opens the stream at the note, a session at its divider, and a combat its
 * page. The stream's filter is left out, so the target is never hidden by one.
 */
export function hitTarget(campaignId: string, hit: SearchHit): SearchTarget {
    const campaign = `/app/campaigns/${encodeURIComponent(campaignId)}`;
    if (hit.entry) {
        const path = `${campaign}/wiki/${encodeURIComponent(hit.entry.entry.id)}`;
        return hit.entry.blockId ? { path, query: { [BLOCK_LINK_PARAM]: hit.entry.blockId } } : { path, query: {} };
    }
    if (hit.note) return { path: campaign, query: { [NOTE_LINK_PARAM]: hit.note.id } };
    if (hit.session) return { path: campaign, query: { [SESSION_LINK_PARAM]: String(hit.session.session.number) } };
    if (hit.combat) return { path: `${campaign}/combat/${encodeURIComponent(hit.combat.combat.id)}`, query: {} };
    return { path: campaign, query: {} };
}

/** `?session=12` as a session number, or undefined. */
export function sessionFromQuery(value: unknown): number | undefined {
    if (typeof value !== "string" || !/^\d{1,9}$/.test(value)) return undefined;
    const number = Number(value);
    return number > 0 ? number : undefined;
}

// ── Recent entries ───────────────────────────────────────────────────────────

export const RECENT_ENTRIES_MAX = 5;
export const recentEntriesKey = (campaignId: string) => `ti:recentEntries:${campaignId}`;

/** The id first, without repeats, at most five. */
export function pushRecent(ids: readonly string[], id: string, max = RECENT_ENTRIES_MAX): string[] {
    const lower = id.toLowerCase();
    return [id, ...ids.filter((i) => i.toLowerCase() !== lower)].slice(0, max);
}

export function readRecentIds(storage: Storage | undefined, campaignId: string): string[] {
    try {
        const parsed: unknown = JSON.parse(storage?.getItem(recentEntriesKey(campaignId)) ?? "[]");
        return Array.isArray(parsed) ? parsed.filter((i): i is string => typeof i === "string").slice(0, RECENT_ENTRIES_MAX) : [];
    } catch {
        return [];
    }
}

export function rememberRecentEntry(storage: Storage | undefined, campaignId: string, entryId: string): string[] {
    const ids = pushRecent(readRecentIds(storage, campaignId), entryId);
    try {
        storage?.setItem(recentEntriesKey(campaignId), JSON.stringify(ids));
    } catch {
        // Storage is full or blocked: recents are a convenience.
    }
    return ids;
}

/**
 * The recent ids as the viewer's entries now, through the directory: an entry now
 * hidden from the viewer, deleted or merged away drops out.
 */
export function recentEntries(ids: readonly string[], directory: EntryDirectory): EntryListItem[] {
    const items: EntryListItem[] = [];
    for (const id of ids) {
        const item = directory.byId.get(id.toLowerCase());
        // A merged id maps to its target: the id itself is gone.
        if (!item || item.entry.id.toLowerCase() !== id.toLowerCase() || items.includes(item)) continue;
        items.push(item);
    }
    return items.slice(0, RECENT_ENTRIES_MAX);
}
