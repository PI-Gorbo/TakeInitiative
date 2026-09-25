// The wiki's pure rules (15c): kind labels and icons, the viewer's entry directory
// (what mention chips and 15d's `@` read), who may change an entry, and the wiki
// home's filtering and sorting. Components only wire these to the DOM.
import type {
    CampaignMember,
    EditAccess,
    Entry,
    EntryChange,
    EntryHistoryItem,
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
 * query) kept fresh by `entryUpserted`, `entryRemoved` and `entryMerged`. An id that is
 * not here is unknown or hidden from the viewer, and its mentions are plain text.
 */
export type EntryDirectory = {
    /**
     * Keyed by lower-case id. A merged entry's id (15g, `mergedFromIds`) maps to the entry
     * it was merged into, so an old mention resolves to the target (invariant 6: the text
     * keeps the old id). `items` holds only live entries.
     */
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
        const byId = new Map<string, EntryListItem>();
        for (const item of list.entries) {
            for (const merged of item.entry.mergedFromIds ?? []) byId.set(merged.toLowerCase(), item);
        }
        // A live id always wins over a merged one.
        for (const item of list.entries) byId.set(item.entry.id.toLowerCase(), item);
        directory = { byId, items: list.entries };
        directories.set(list, directory);
    }
    return directory;
}

/**
 * The entry a mention's id points to, or undefined when the viewer cannot see it. A
 * merged id gives the entry it was merged into (15g), so a chip links to the target
 * and keeps the text its author wrote.
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

// ── Player characters and stats (15g, the API's EntryStats) ──────────────────

/** The member whose player character this is, if any. */
export const claimerOf = (entry: Pick<EntrySummary, "claimedByMemberId">) => entry.claimedByMemberId ?? null;

/** "Claim as my character": an unclaimed Character. */
export const canClaimEntry = (entry: Pick<EntrySummary, "kind" | "claimedByMemberId">) =>
    entry.kind === "Character" && !claimerOf(entry);

/** Unclaim: the claimer and the DMs. */
export const canUnclaimEntry = (entry: Pick<EntrySummary, "claimedByMemberId">, viewer: EntryViewer) =>
    !!claimerOf(entry) && (viewer.isDm || claimerOf(entry) === viewer.memberId);

/** A DM assigns any Character to any member who can see it. */
export const canAssignClaim = (entry: Pick<EntrySummary, "kind">, viewer: EntryViewer) =>
    viewer.isDm && entry.kind === "Character";

/**
 * Who can read stats: on a claimed Character everyone who sees it, on an unclaimed
 * one the DMs only. The API leaves `stats` out for everyone else, the same as none.
 */
export const canReadStats = (entry: Pick<EntrySummary, "kind" | "claimedByMemberId">, viewer: EntryViewer) =>
    entry.kind === "Character" && (!!claimerOf(entry) || viewer.isDm);

/** Who writes stats: the claimer and the DMs; on an unclaimed Character the DMs only. */
export const canWriteStats = (entry: Pick<EntrySummary, "kind" | "claimedByMemberId">, viewer: EntryViewer) =>
    entry.kind === "Character" && (viewer.isDm || (!!claimerOf(entry) && claimerOf(entry) === viewer.memberId));

/** The API's limits for a stat line. */
export const STATS_AC_MAX = 99;
export const STATS_EXPRESSION_MAX = 100;

/** The stats editor's three text fields. */
export type StatsForm = { initiativeRoll: string; maxHp: string; ac: string };

/**
 * The stats editor's fields as a `PUT stats` body: blank is none (all blank clears
 * the stats), and AC must be a whole number 0–99. The dice expressions are checked by
 * the server, whose message shows under the field. `body` is null when a field is wrong.
 */
export function parseStatsForm(form: StatsForm): {
    body: { initiativeRoll: string | null; maxHp: string | null; ac: number | null } | null;
    errors: Partial<Record<keyof StatsForm, string>>;
} {
    const errors: Partial<Record<keyof StatsForm, string>> = {};
    const expression = (key: "initiativeRoll" | "maxHp") => {
        const value = form[key].trim();
        if (value.length > STATS_EXPRESSION_MAX) errors[key] = `At most ${STATS_EXPRESSION_MAX} characters.`;
        return value === "" ? null : value;
    };
    const initiativeRoll = expression("initiativeRoll");
    const maxHp = expression("maxHp");
    const acText = form.ac.trim();
    let ac: number | null = null;
    if (acText !== "") {
        ac = /^\d{1,2}$/.test(acText) ? Number(acText) : Number.NaN;
        if (Number.isNaN(ac) || ac < 0 || ac > STATS_AC_MAX) errors.ac = `AC must be between 0 and ${STATS_AC_MAX}.`;
    }
    return Object.keys(errors).length > 0 ? { body: null, errors } : { body: { initiativeRoll, maxHp, ac }, errors };
}

/** "Initiative 1d20+2 · HP 3d10 · AC 17", or "" with no stats. */
export function statsLabel(stats: Entry["stats"]): string {
    if (!stats) return "";
    return [
        stats.initiativeRoll ? `Initiative ${stats.initiativeRoll}` : "",
        stats.maxHp ? `HP ${stats.maxHp}` : "",
        stats.ac != null ? `AC ${stats.ac}` : "",
    ]
        .filter(Boolean)
        .join(" · ");
}

// ── History (15g) ────────────────────────────────────────────────────────────

/** Whether a change is a version of the article that "Restore this version" can bring back. */
export const isArticleVersion = (change: Pick<EntryChange, "type" | "blocks">) =>
    (change.type === "ArticleEdited" || change.type === "QuotePromoted" || change.type === "Merged") && !!change.blocks;

/** The index of the last article version in a history: the current article, not restorable. */
export const currentVersionIndex = (items: readonly Pick<EntryHistoryItem, "change">[]) =>
    items.findLastIndex((item) => isArticleVersion(item.change));

/** One line for a change, after the actor's name: "renamed it to "Gundren Rockseeker"". */
export function describeChange(change: EntryChange, nameOf: (memberId: string) => string): string {
    switch (change.type) {
        case "Created":
            return `created it as a ${change.kind ?? "entry"}${change.visibility && change.visibility !== "Everyone" ? ` (🔒 ${change.visibility})` : ""}`;
        case "Renamed":
            return `renamed it to "${change.name}"`;
        case "KindChanged":
            return `made it a ${change.kind}`;
        case "AliasAdded":
            return `added the alias "${change.alias}"`;
        case "AliasRemoved":
            return `removed the alias "${change.alias}"`;
        case "VisibilityChanged":
            return change.visibility === "Everyone"
                ? "made it visible to everyone"
                : `made it 🔒 ${change.visibility}`;
        case "EditAccessChanged":
            return change.editAccess === "OnlyMe" ? "set edit access to Only the creator" : "set edit access to Anyone";
        case "ArticleEdited":
            return "edited the article";
        case "QuotePromoted":
            return "promoted a quote";
        case "Merged":
            return `merged "${change.name}" into it`;
        case "Claimed":
            return change.memberId ? `made it ${nameOf(change.memberId)}'s player character` : "claimed it";
        case "Unclaimed":
            return "unclaimed it";
        case "StatsChanged":
            return change.stats ? `set the stats: ${statsLabel(change.stats)}` : "cleared the stats";
    }
}

// ── Merge (15g, the API's EntryMerge) ────────────────────────────────────────

type AudienceEntry = Pick<EntrySummary, "visibility" | "creatorMemberId">;
type AudienceMember = Pick<CampaignMember, "memberId" | "role">;

/** Whether a member can see an entry: the API's `EntryVisibility.CanSee`. */
export function entryVisibleTo(entry: AudienceEntry, member: AudienceMember): boolean {
    if (member.memberId === entry.creatorMemberId) return true;
    switch (entry.visibility) {
        case "Everyone":
            return true;
        case "DM":
            return member.role === "DM";
        default:
            return false;
    }
}

/**
 * The merge guard: refused unless everyone who can see the target can already see the
 * merged entry. Otherwise its name (an alias after the merge) and its article would reach
 * people who could not see them. The API checks the same with the current members.
 */
export const mergeRevealsNothing = (from: AudienceEntry, into: AudienceEntry, members: readonly AudienceMember[]) =>
    members.every((m) => !entryVisibleTo(into, m) || entryVisibleTo(from, m));

/** The members who can see the merged entry but not the target: "Players will no longer see …". */
export const mergeLosers = <M extends AudienceMember>(from: AudienceEntry, into: AudienceEntry, members: readonly M[]) =>
    members.filter((m) => entryVisibleTo(from, m) && !entryVisibleTo(into, m));

/**
 * Why a merge would be refused, in the API's words, or null when it would go through.
 * Mirrors `EntryMerge.Check` so the dialog can say so before the request.
 */
export function mergeProblem(
    from: AudienceEntry & Pick<EntrySummary, "name" | "claimedByMemberId" | "aliases">,
    into: AudienceEntry & Pick<EntrySummary, "name" | "kind" | "claimedByMemberId" | "aliases">,
    members: readonly AudienceMember[]
): string | null {
    if (!mergeRevealsNothing(from, into, members)) {
        return `Some people who can see "${into.name}" cannot see "${from.name}". Change visibility first.`;
    }
    const claimer = claimerOf(from);
    if (claimer) {
        if (into.kind !== "Character") return `"${from.name}" is a player character, so it can only be merged into a Character.`;
        const other = claimerOf(into);
        if (other && other !== claimer) return `"${from.name}" and "${into.name}" are different members' player characters.`;
    }
    const aliases = [from.name, ...from.aliases].reduce(
        (acc, alias) => addAlias(acc, alias, into.name, Number.POSITIVE_INFINITY),
        [...into.aliases]
    );
    if (aliases.length > ENTRY_ALIASES_MAX) {
        return `"${into.name}" would have ${aliases.length} aliases, and at most ${ENTRY_ALIASES_MAX} are allowed. Remove some first.`;
    }
    return null;
}

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

// `aboutPrefill` is in `utils/mentions.ts` (15d): it writes `@[Name]` plus a link.

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
export function addAlias(aliases: readonly string[], alias: string, name: string, max = ENTRY_ALIASES_MAX): string[] {
    const value = alias.trim();
    const key = normalizeName(value);
    if (!key || key === normalizeName(name) || aliases.length >= max) return [...aliases];
    if (aliases.some((a) => normalizeName(a) === key)) return [...aliases];
    return [...aliases, value];
}
