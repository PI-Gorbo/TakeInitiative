// The wiki's pure rules (15c): kind labels and icons, the viewer's entry directory
// (what mention chips and 15d's `@` read), who may change an entry, and the wiki
// home's filtering and sorting. Components only wire these to the DOM.
import type {
    EditAccess,
    EntryKind,
    EntryList,
    EntryListItem,
    EntrySummary,
    Visibility,
} from "./api/types";

// ── Kinds ────────────────────────────────────────────────────────────────────

/** Glossary §1: a closed set, in this order everywhere. */
export const ENTRY_KINDS = [
    { value: "Character", label: "Character", icon: "👤" },
    { value: "Place", label: "Place", icon: "📍" },
    { value: "Faction", label: "Faction", icon: "🛡️" },
    { value: "Item", label: "Item", icon: "🗝️" },
    { value: "Event", label: "Event", icon: "📅" },
    { value: "Other", label: "Other", icon: "📄" },
] as const satisfies readonly { value: EntryKind; label: string; icon: string }[];

export const ENTRY_KIND_ICONS = Object.fromEntries(ENTRY_KINDS.map((k) => [k.value, k.icon])) as Record<
    EntryKind,
    string
>;

// ── The entry directory ──────────────────────────────────────────────────────

/**
 * Every entry the viewer can see, from `GET entries` (the `["entries", campaignId]`
 * query) kept fresh by `entryUpserted` and `entryRemoved`. An id that is not here is
 * unknown or hidden from the viewer, and its mentions are plain text.
 */
export type EntryDirectory = {
    /** Keyed by lower-case id. */
    byId: ReadonlyMap<string, EntryListItem>;
    items: readonly EntryListItem[];
};

const EMPTY_DIRECTORY: EntryDirectory = { byId: new Map(), items: [] };
const directories = new WeakMap<EntryList, EntryDirectory>();

/** The directory for a loaded list. Memoised per list object, so every chip shares one. */
export function entryDirectory(list: EntryList | undefined): EntryDirectory {
    if (!list) return EMPTY_DIRECTORY;
    let directory = directories.get(list);
    if (!directory) {
        directory = {
            byId: new Map(list.entries.map((item) => [item.entry.id.toLowerCase(), item])),
            items: list.entries,
        };
        directories.set(list, directory);
    }
    return directory;
}

/**
 * The entry a mention's id points to, or undefined when the viewer cannot see it.
 * 15g follows merges here (`mergedFromIds`), so a chip links to the merge target.
 */
export function resolveEntry(directory: EntryDirectory, entryId: string): EntrySummary | undefined {
    return directory.byId.get(entryId.toLowerCase())?.entry;
}

const normalizeName = (name: string) => name.trim().toLocaleLowerCase();

/**
 * The entries whose name or an alias equals `name`, ignoring case: the API's
 * `EntryNameRules.IsCalled`. 15d uses it to decide whether to offer Create "…".
 */
export function entriesCalled(directory: EntryDirectory, name: string): EntryListItem[] {
    const wanted = normalizeName(name);
    if (!wanted) return [];
    return directory.items.filter(
        ({ entry }) => normalizeName(entry.name) === wanted || entry.aliases.some((a) => normalizeName(a) === wanted)
    );
}

// ── Permissions (the API's EntryPermissions, 15a) ────────────────────────────

export type EntryViewer = { memberId: string; isDm: boolean };

/** Name, kind and aliases: a DM, the creator, or anyone who can see it when `Anyone`. */
export const canEditEntry = (entry: Pick<EntrySummary, "creatorMemberId" | "editAccess">, viewer: EntryViewer) =>
    viewer.isDm || entry.creatorMemberId === viewer.memberId || entry.editAccess === "Anyone";

/** Visibility and edit access: the creator and the DMs. */
export const canChangeEntryAccess = (entry: Pick<EntrySummary, "creatorMemberId">, viewer: EntryViewer) =>
    viewer.isDm || entry.creatorMemberId === viewer.memberId;

/** An entry's visibility options. `DM` and `Me` are relative to the creator. */
export const ENTRY_VISIBILITY_OPTIONS = [
    { value: "Everyone", label: "Everyone", hint: "Every member of the campaign" },
    { value: "DM", label: "🔒 DM", hint: "The DMs and the creator" },
    { value: "Me", label: "🔒 Me", hint: "Only the creator" },
] as const satisfies readonly { value: Visibility; label: string; hint: string }[];

export const EDIT_ACCESS_OPTIONS = [
    { value: "Anyone", label: "Anyone", hint: "Anyone who can see it" },
    { value: "OnlyMe", label: "Only me", hint: "Only the creator. DMs can always edit" },
] as const satisfies readonly { value: EditAccess; label: string; hint: string }[];

/** "Anyone", "Only me" (to the creator), or "Only Sam" (to everyone else). */
export function editAccessLabel(entry: Pick<EntrySummary, "creatorMemberId" | "editAccess">, viewerMemberId: string, creatorName: string) {
    if (entry.editAccess === "Anyone") return "Anyone";
    return entry.creatorMemberId === viewerMemberId ? "Only me" : `Only ${creatorName}`;
}

// ── Wiki home: kind, filter and sort ─────────────────────────────────────────

export type WikiSort = "mentions" | "recent" | "name";

export const WIKI_SORTS = [
    { value: "mentions", label: "Most mentioned" },
    { value: "recent", label: "Recently mentioned" },
    { value: "name", label: "A–Z" },
] as const satisfies readonly { value: WikiSort; label: string }[];

export const SORT_PARAM = "sort";
export const KIND_PARAM = "kind";

/** `?sort=recent` → `recent`. Missing or unknown values are the default, Most mentioned. */
export function sortFromQuery(value: unknown): WikiSort {
    const raw = Array.isArray(value) ? value[0] : value;
    return WIKI_SORTS.find((s) => s.value === raw)?.value ?? "mentions";
}

/** The default sort is no parameter, so the plain URL stays plain. */
export const sortToQuery = (sort: WikiSort): string | undefined => (sort === "mentions" ? undefined : sort);

/** `?kind=place` → `Place`. Missing or unknown values are All (null). */
export function kindFromQuery(value: unknown): EntryKind | null {
    const raw = Array.isArray(value) ? value[0] : value;
    if (typeof raw !== "string") return null;
    return ENTRY_KINDS.find((k) => k.value.toLowerCase() === raw.toLowerCase())?.value ?? null;
}

export const kindToQuery = (kind: EntryKind | null): string | undefined => kind?.toLowerCase();

/** The entries of one kind (null for all) whose name or an alias contains `query`. */
export function filterEntries(
    items: readonly EntryListItem[],
    { kind, query }: { kind: EntryKind | null; query: string }
): EntryListItem[] {
    const wanted = normalizeName(query);
    return items.filter(
        ({ entry }) =>
            (kind === null || entry.kind === kind) &&
            (!wanted ||
                entry.name.toLocaleLowerCase().includes(wanted) ||
                entry.aliases.some((a) => a.toLocaleLowerCase().includes(wanted)))
    );
}

const byName = (a: EntryListItem, b: EntryListItem) =>
    a.entry.name.localeCompare(b.entry.name, undefined, { sensitivity: "base" }) || a.entry.id.localeCompare(b.entry.id);

const lastMentioned = (item: EntryListItem) => (item.lastMentionedAt ? Date.parse(item.lastMentionedAt) : -Infinity);

/**
 * Most mentioned: count, then the most recent mention, then A–Z. Recently mentioned:
 * the most recent mention, never-mentioned last, then A–Z. A–Z ignores case.
 */
export function sortEntries(items: readonly EntryListItem[], sort: WikiSort): EntryListItem[] {
    const sorted = [...items];
    switch (sort) {
        case "mentions":
            return sorted.sort(
                (a, b) => b.mentionCount - a.mentionCount || lastMentioned(b) - lastMentioned(a) || byName(a, b)
            );
        case "recent":
            return sorted.sort((a, b) => {
                const diff = lastMentioned(b) - lastMentioned(a);
                return (Number.isNaN(diff) ? 0 : diff) || byName(a, b);
            });
        case "name":
            return sorted.sort(byName);
    }
}

/** "No mentions", "1 mention", "7 mentions". */
export const mentionCountLabel = (count: number) =>
    count === 0 ? "No mentions" : count === 1 ? "1 mention" : `${count} mentions`;

/** `aka "Rockseeker", "the dwarf"`, or "" with no aliases. */
export const aliasesLabel = (aliases: readonly string[]) =>
    aliases.length === 0 ? "" : `aka ${aliases.map((a) => `"${a}"`).join(", ")}`;

// ── Mentions in the stored form ──────────────────────────────────────────────

// Characters that would change how the link text reads as markdown.
const MARKDOWN_SPECIALS = /[\\`*_[\]<>~&]/g;

/**
 * An entry's mention in the stored form, `@[Name](entry:<id>)`, with the name escaped
 * so it reads back as exactly that text.
 */
export const mentionMarkup = (name: string, entryId: string) =>
    `@[${name.replace(MARKDOWN_SPECIALS, (c) => `\\${c}`)}](entry:${entryId})`;

// ── "Add a note about X" ─────────────────────────────────────────────────────

/** `/app/campaigns/{id}?about={entryId}`: the composer starts with a mention of it. */
export const ABOUT_PARAM = "about";

/**
 * The composer's start for a note about an entry: the text begins with its mention
 * (15d turns the stored form into `@[Name]`), and a `DM` or `Me` entry makes the note
 * match, so the reveal warning (15d) does not fire on the note's own subject.
 */
export function aboutPrefill(
    currentText: string,
    entry: Pick<EntrySummary, "id" | "name" | "visibility">
): { text: string; visibility: Visibility | undefined } {
    const mention = `${mentionMarkup(entry.name, entry.id)} `;
    const rest = currentText.trimStart();
    return {
        text: rest.startsWith(mention.trimEnd()) ? currentText : mention + rest,
        visibility: entry.visibility === "Everyone" ? undefined : entry.visibility,
    };
}

// ── Errors ───────────────────────────────────────────────────────────────────

/**
 * The id in a duplicate-name 409 (`errors.existingEntryId[0]`, 15a), so the dialog
 * can link to the entry that already has the name. Null for any other error.
 */
export function existingEntryIdFrom(error: unknown): string | null {
    const errors = (error as { response?: { data?: { errors?: Record<string, unknown> } } } | undefined)?.response?.data
        ?.errors;
    const ids = errors?.existingEntryId;
    return Array.isArray(ids) && typeof ids[0] === "string" ? ids[0] : null;
}

// ── Aliases ──────────────────────────────────────────────────────────────────

/** The API's limit (15a). */
export const ENTRY_ALIASES_MAX = 20;

/**
 * The aliases with one more: trimmed, and left out when blank, equal to the name, or
 * already there (ignoring case), or when the list is full. The API applies the same
 * rules (`EntryNameRules.NormalizeAliases`).
 */
export function addAlias(aliases: readonly string[], alias: string, name: string): string[] {
    const value = alias.trim();
    const key = normalizeName(value);
    if (!key || key === normalizeName(name) || aliases.length >= ENTRY_ALIASES_MAX) return [...aliases];
    if (aliases.some((a) => normalizeName(a) === key)) return [...aliases];
    return [...aliases, value];
}
