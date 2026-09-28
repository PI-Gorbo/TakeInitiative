// ⌘K's actions (17c): the registry, which actions a query offers and in what order,
// the Create row's kind and visibility cycling, `?compose=` and `?new=` (18f). Pure:
// an action's `run` describes what to do, and `SearchSheet` does it.
import type { EntryKind, EntrySummary, Visibility } from "./api/types";
import {
    ABOUT_PARAM,
    ENTRY_KINDS,
    ENTRY_VISIBILITY_OPTIONS,
    KIND_PARAM,
    entriesCalled,
    type EntryDirectory,
} from "./entries";
import { foldForMatch, matchRank } from "./mentions";
import { COMBAT_NAME_MAX } from "./combat";
import type { SearchScope, SearchTarget } from "./search";
import { FILTER_PARAM } from "./streamFilters";

// ── The context ──────────────────────────────────────────────────────────────

export type SearchActionContext = {
    campaignId: string;
    /** The viewer's role: "⚔ Start combat" (18f) is DMs only. */
    isDm: boolean;
    /** Current + 1, or null while the sessions are not loaded (Start is hidden). */
    nextSessionNumber: number | null;
    /** Whether the campaign has a session yet: a combat starts in the current one (18f). */
    hasSession: boolean;
    scope: SearchScope;
    /** The query text, after its prefix. */
    text: string;
    directory: EntryDirectory;
    /** The highlighted entry hit, else the top one; null when there is none. */
    entryHit: Pick<EntrySummary, "id" | "name"> | null;
    /** The viewer's loose ends (19e); "Loose ends (n)" shows when there are any. */
    looseEndCount: number;
};

/** What choosing an action does. `composer` hands the focus to the composer. */
export type SearchActionRun =
    | { kind: "navigate"; target: SearchTarget; composer?: boolean }
    | { kind: "startSession"; number: number }
    | { kind: "createEntry"; name: string };

export type SearchAction = {
    id: string;
    icon: string;
    label: (context: SearchActionContext) => string;
    /** Matched with the label; they never show. */
    keywords: (context: SearchActionContext) => readonly string[];
    available: (context: SearchActionContext) => boolean;
    run: (context: SearchActionContext) => SearchActionRun;
    /**
     * Built from the query ("Create entry "X"", "New note mentioning "X""), so it is
     * not matched against it. These are offered in the `all` and `@` scopes only.
     */
    fromQuery?: boolean;
    /**
     * Offered for any text in the `all` scope, in a slot the query's own and the
     * matched actions leave free, as well as when it matches (design §7's
     * "gund" → "⚔ Start combat").
     */
    withAnyText?: boolean;
};

const campaignPath = (campaignId: string) => `/app/campaigns/${encodeURIComponent(campaignId)}`;
const to = (path: string, query: Record<string, string> = {}): SearchActionRun => ({
    kind: "navigate",
    target: { path, query },
});

// ── The registry ─────────────────────────────────────────────────────────────

export const CREATE_ENTRY_ACTION_ID = "create-entry";
export const START_SESSION_ACTION_ID = "start-session";
export const START_COMBAT_ACTION_ID = "start-combat";
export const LOOSE_ENDS_ACTION_ID = "loose-ends";
export const COMPOSE_PARAM = "compose";
/** `combat?new=Goblin Ambush`: the Combat tab opens New combat with that name (18f). */
export const NEW_COMBAT_PARAM = "new";
/** The most `?compose=` puts in the composer. */
export const COMPOSE_TEXT_MAX = 200;

const WIKI_KIND_LABELS: Record<EntryKind, string> = {
    Character: "Characters",
    Place: "Places",
    Faction: "Factions",
    Item: "Items",
    Event: "Events",
    Other: "Other",
};

const TABS = [
    { id: "campaign", label: "Campaign", path: "" },
    { id: "wiki", label: "Wiki", path: "/wiki" },
    { id: "combat", label: "Combat", path: "/combat" },
] as const;

const FILTERS = [
    { id: "recaps", label: "Show recaps", param: "recaps", keywords: ["recaps", "filter"] },
    { id: "images", label: "Show images", param: "images", keywords: ["images", "pictures", "photos", "filter"] },
    { id: "mine", label: "Show my notes", param: "mine", keywords: ["mine", "my notes", "filter"] },
] as const;

export const SEARCH_ACTIONS: readonly SearchAction[] = [
    {
        id: CREATE_ENTRY_ACTION_ID,
        icon: "＋",
        label: ({ text }) => `Create entry “${text}”`,
        keywords: () => [],
        // Not when the text is already the name or an alias of an entry the viewer can
        // see. A hidden one of the same name does not block it (15g merges it later).
        available: ({ text, directory }) => !!text && entriesCalled(directory, text).length === 0,
        run: ({ text }) => ({ kind: "createEntry", name: text }),
        fromQuery: true,
    },
    {
        id: "note-about",
        icon: "✎",
        label: ({ entryHit }) => `Post a note about ${entryHit?.name ?? ""}`,
        keywords: () => [],
        available: ({ entryHit }) => !!entryHit,
        run: ({ campaignId, entryHit }) => ({
            ...to(campaignPath(campaignId), { [ABOUT_PARAM]: entryHit!.id }),
            composer: true,
        }),
        fromQuery: true,
    },
    {
        id: "note-mentioning",
        icon: "✎",
        label: ({ text }) => `New note mentioning “${text}”`,
        keywords: () => [],
        available: ({ text }) => !!text,
        run: ({ campaignId, text }) => ({
            ...to(campaignPath(campaignId), { [COMPOSE_PARAM]: `@${text}` }),
            composer: true,
        }),
        fromQuery: true,
    },
    {
        id: START_SESSION_ACTION_ID,
        icon: "▶",
        label: ({ nextSessionNumber }) => `Start Session ${nextSessionNumber}`,
        keywords: ({ nextSessionNumber: n }) => ["new session", "next session", `session ${n}`, `s${n}`],
        available: ({ nextSessionNumber }) => nextSessionNumber !== null,
        run: ({ nextSessionNumber }) => ({ kind: "startSession", number: nextSessionNumber! }),
    },
    {
        id: START_COMBAT_ACTION_ID,
        icon: "⚔",
        label: () => "Start combat",
        keywords: () => ["new combat", "fight", "encounter", "initiative", "battle"],
        // DMs only, and a combat is created in the current session, so one must exist.
        available: ({ isDm, hasSession }) => isDm && hasSession,
        run: (context) =>
            to(`${campaignPath(context.campaignId)}/combat`, { [NEW_COMBAT_PARAM]: startCombatName(context) }),
        withAnyText: true,
    },
    ...TABS.map(
        (tab): SearchAction => ({
            id: `go-${tab.id}`,
            icon: "→",
            label: () => `Go to ${tab.label}`,
            keywords: () => [tab.label, "tab"],
            available: () => true,
            run: ({ campaignId }) => to(`${campaignPath(campaignId)}${tab.path}`),
        })
    ),
    ...ENTRY_KINDS.map(
        (kind): SearchAction => ({
            id: `wiki-${kind.value.toLowerCase()}`,
            icon: kind.icon,
            label: () => `Wiki: ${WIKI_KIND_LABELS[kind.value]}`,
            keywords: () => [WIKI_KIND_LABELS[kind.value], kind.label],
            available: () => true,
            run: ({ campaignId }) => to(`${campaignPath(campaignId)}/wiki`, { [KIND_PARAM]: kind.value.toLowerCase() }),
        })
    ),
    {
        id: "wiki-graph",
        icon: "🕸️",
        label: () => "Wiki: Graph",
        keywords: () => ["graph", "connections"],
        available: () => true,
        run: ({ campaignId }) => to(`${campaignPath(campaignId)}/wiki/graph`),
    },
    {
        id: LOOSE_ENDS_ACTION_ID,
        icon: "🧵",
        label: ({ looseEndCount }) => `Loose ends (${looseEndCount})`,
        keywords: () => ["loose", "untagged", "unlinked", "todo"],
        available: ({ looseEndCount }) => looseEndCount > 0,
        run: ({ campaignId }) => to(`${campaignPath(campaignId)}/wiki/loose-ends`),
    },
    ...FILTERS.map(
        (filter): SearchAction => ({
            id: `filter-${filter.id}`,
            icon: "⧩",
            label: () => filter.label,
            keywords: () => filter.keywords,
            available: () => true,
            run: ({ campaignId }) => to(campaignPath(campaignId), { [FILTER_PARAM]: filter.param }),
        })
    ),
];

// ── Which actions a query offers ─────────────────────────────────────────────

/** In the `all` and `@` scopes, Actions is the last section, with at most this many. */
export const SEARCH_ACTIONS_MAX = 4;
/** What an empty input offers: Start Session N+1, Start combat (DMs), then the tabs. */
const DEFAULT_ACTION_IDS = [START_SESSION_ACTION_ID, START_COMBAT_ACTION_ID, "go-campaign", "go-wiki", "go-combat"];

/** An action's best `matchRank` over its label and keywords, or undefined. */
export function actionRank(action: SearchAction, context: SearchActionContext, text: string): number | undefined {
    const query = foldForMatch(text);
    const ranks = [action.label(context), ...action.keywords(context)]
        .map((candidate) => matchRank(candidate, query))
        .filter((rank): rank is number => rank !== undefined);
    return ranks.length > 0 ? Math.min(...ranks) : undefined;
}

/**
 * The actions for the input, in order:
 * - `>`: every available action but the query's own, matched by the text, best first;
 * - `all`, empty: the defaults;
 * - `all` with text: Create, Post a note about, New note mentioning, then the matched
 *   actions, then Start combat if there is room, at most four in all;
 * - `@`: only the query's own actions (the rest are not about entries).
 */
export function searchActions(
    context: SearchActionContext,
    registry: readonly SearchAction[] = SEARCH_ACTIONS
): SearchAction[] {
    const available = registry.filter((action) => action.available(context));
    const matched = () =>
        available
            .filter((action) => !action.fromQuery)
            .map((action, index) => ({ action, index, rank: actionRank(action, context, context.text) }))
            .filter((m): m is typeof m & { rank: number } => m.rank !== undefined)
            .sort((a, b) => a.rank - b.rank || a.index - b.index)
            .map((m) => m.action);

    if (context.scope === "actions") return matched();
    if (!context.text) {
        if (context.scope === "entries") return [];
        return DEFAULT_ACTION_IDS.map((id) => available.find((a) => a.id === id)).filter(
            (a): a is SearchAction => !!a
        );
    }
    const own = available.filter((action) => action.fromQuery);
    if (context.scope === "entries") return own.slice(0, SEARCH_ACTIONS_MAX);
    const found = matched();
    const anyText = available.filter((action) => action.withAnyText && !found.includes(action));
    return [...own, ...found, ...anyText].slice(0, SEARCH_ACTIONS_MAX);
}

/**
 * The name Start combat hands the New combat dialog: the query text in the `all`
 * scope ("goblin ambush" → "goblin ambush"), unless the text is the action's own
 * words ("start combat", "fight"), and nothing from `>` (the dialog's default).
 */
export function startCombatName(context: SearchActionContext): string {
    if (context.scope !== "all" || !context.text) return "";
    const action = SEARCH_ACTIONS.find((a) => a.id === START_COMBAT_ACTION_ID);
    if (action && actionRank(action, context, context.text) !== undefined) return "";
    return context.text.slice(0, COMBAT_NAME_MAX);
}

// ── The Create row ───────────────────────────────────────────────────────────

const cycle = <T>(values: readonly T[], current: T, step: 1 | -1): T =>
    values[(values.indexOf(current) + step + values.length) % values.length]!;

/** Tab on the Create row: the next kind, as in the composer (§3). */
export const cycleEntryKind = (kind: EntryKind, step: 1 | -1 = 1): EntryKind =>
    cycle(
        ENTRY_KINDS.map((k) => k.value),
        kind,
        step
    );

/** Shift+Tab on the Create row: the next visibility. */
export const cycleEntryVisibility = (visibility: Visibility, step: 1 | -1 = 1): Visibility =>
    cycle(
        ENTRY_VISIBILITY_OPTIONS.map((v) => v.value),
        visibility,
        step
    );

// ── `?compose=` ──────────────────────────────────────────────────────────────

/** `?compose=@Klarg` as the composer's text: trimmed, at most 200 characters; else undefined. */
export function composeFromQuery(value: unknown): string | undefined {
    const raw = Array.isArray(value) ? value[0] : value;
    if (typeof raw !== "string") return undefined;
    const text = raw.trim().slice(0, COMPOSE_TEXT_MAX);
    return text || undefined;
}

// ── `?new=` ──────────────────────────────────────────────────────────────────

/**
 * `combat?new=Goblin Ambush` as the New combat dialog's name: trimmed, at most 100
 * characters, and "" for an empty value (the dialog's default). Undefined when the
 * parameter is absent.
 */
export function newCombatFromQuery(value: unknown): string | undefined {
    const raw = Array.isArray(value) ? value[0] : value;
    if (raw === null && value !== undefined) return "";
    if (typeof raw !== "string") return undefined;
    return raw.trim().slice(0, COMBAT_NAME_MAX);
}

/**
 * Whether `?compose=` may fill the composer: only an empty one. A draft (text or
 * images) is never overwritten, and neither is a note being edited.
 */
export const composeFits = (composer: { text: string; attachmentCount: number; editing: boolean }) =>
    !composer.editing && composer.text.trim() === "" && composer.attachmentCount === 0;
