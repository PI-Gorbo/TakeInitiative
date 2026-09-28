// Combat v2 on the web (18c): the order, the turn, HP as each viewer may see it, what
// each role can do, the Combat tab's list and the `@Goblin ×4` add box. Pure, so it is
// unit tested (`tests/unit/combat.test.ts`). The server does every sort and placement:
// combatants arrive in the initiative order, then the waiting ones in the order added.
import type {
    Combat,
    Combatant,
    CombatantRequest,
    CombatList,
    CombatStatus,
    CombatSummary,
    EntrySummary,
    HpBand,
} from "./api/types";
import { resolveEntry, type EntryDirectory, type EntryViewer } from "./entries";

/** The API's limits (18a.1). */
export const COMBAT_NAME_MAX = 100;
export const COMBATANT_COUNT_MAX = 20;
export const COMBATANTS_MAX = 50;
export const COMBAT_AC_MAX = 99;
export const COMBAT_EXPRESSION_MAX = 100;
/** The Finished section shows this many, then "Show more". */
export const FINISHED_PAGE_SIZE = 20;

const sameId = (a: string | null | undefined, b: string | null | undefined) =>
    !!a && !!b && a.toLowerCase() === b.toLowerCase();

// ── The add box: `goblin ×4`, `goblin x4`, `@Goblin ×4` ──────────────────────

export type AddCombatantsQuery = {
    /** The name to match, with no `@` and no count. */
    query: string;
    /** 1–20: clamped, and 1 when none was typed. */
    count: number;
    /** Whether the text carried a count. */
    hasCount: boolean;
};

// A count after `×`, or after an `x` or `*` that follows a space: "goblin ×4",
// "goblin x 4", "goblin×4". "Goblinx4" is a name, since an x may end one.
const COUNT = /^(.*?)(?:\s*×\s*|\s+[x*]\s*|\s*\*\s*)(\d{1,4})$/i;

/** The add box's text as a name and a count, for 15d's matching (18c.5). */
export function parseAddCombatants(text: string): AddCombatantsQuery {
    let rest = text.trim().replace(/^@+/, "").trim();
    const match = COUNT.exec(rest);
    // A `×` typed before its number yet is not part of the name.
    if (!match)
        return { query: rest.replace(/\s*×$/, ""), count: 1, hasCount: false };
    rest = match[1]!.trim();
    const count = Math.min(COMBATANT_COUNT_MAX, Math.max(1, Number(match[2])));
    return { query: rest, count, hasCount: true };
}

// ── Staged picks ─────────────────────────────────────────────────────────────

/** A pick waiting in the add dialog: an entry (with its Stats as defaults) or a name. */
export type StagedCombatant = {
    key: string;
    entryId: string | null;
    name: string;
    count: number;
    /** A dice expression, rolled once per copy. Blank takes the entry's Stats, or none. */
    maxHp: string;
    ac: string;
    /** Blank takes the entry's Stats, or `1d20`. */
    initiativeRoll: string;
    /** Whether the entry's Stats are loaded, and whether it had any. */
    stats: "loading" | "none" | "some";
};

/**
 * Adds a pick to the staged list. Picking an entry already staged adds to its count
 * (capped at 20), so `goblin ×2` then `goblin ×3` is five goblins in one row.
 */
export function stageCombatant(
    staged: readonly StagedCombatant[],
    pick: Omit<StagedCombatant, "key">,
    key: string
): StagedCombatant[] {
    const index = pick.entryId
        ? staged.findIndex((s) => sameId(s.entryId, pick.entryId))
        : -1;
    if (index < 0) return [...staged, { ...pick, key }];
    return staged.map((s, i) =>
        i === index
            ? {
                  ...s,
                  count: Math.min(COMBATANT_COUNT_MAX, s.count + pick.count),
              }
            : s
    );
}

/** How many combatants the staged list adds. */
export const stagedTotal = (staged: readonly StagedCombatant[]) =>
    staged.reduce((sum, s) => sum + s.count, 0);

/**
 * The staged list as one `POST combatants` body. A blank field is left out, so the
 * server takes the entry's Stats (18a.3). `error` names the first field that is wrong.
 */
export function combatantRequests(staged: readonly StagedCombatant[]): {
    combatants: CombatantRequest[];
    error: string | null;
} {
    const combatants: CombatantRequest[] = [];
    for (const s of staged) {
        const maxHp = s.maxHp.trim();
        const initiativeRoll = s.initiativeRoll.trim();
        const acText = s.ac.trim();
        if (
            maxHp.length > COMBAT_EXPRESSION_MAX ||
            initiativeRoll.length > COMBAT_EXPRESSION_MAX
        )
            return {
                combatants: [],
                error: `${s.name}: at most ${COMBAT_EXPRESSION_MAX} characters.`,
            };
        let ac: number | null = null;
        if (acText !== "") {
            ac = /^\d{1,2}$/.test(acText) ? Number(acText) : Number.NaN;
            if (Number.isNaN(ac) || ac > COMBAT_AC_MAX)
                return {
                    combatants: [],
                    error: `${s.name}: AC must be between 0 and ${COMBAT_AC_MAX}.`,
                };
        }
        combatants.push({
            ...(s.entryId ? { entryId: s.entryId } : { name: s.name }),
            count: s.count,
            ...(maxHp ? { maxHp } : {}),
            ...(ac !== null ? { ac } : {}),
            ...(initiativeRoll ? { initiativeRoll } : {}),
        });
    }
    return { combatants, error: null };
}

// ── The order and the turn ───────────────────────────────────────────────────

/** The initiative order and the waiting combatants, each in the server's order. */
export function splitCombatants(combat: Pick<Combat, "combatants">): {
    ordered: Combatant[];
    waiting: Combatant[];
} {
    const ordered: Combatant[] = [];
    const waiting: Combatant[] = [];
    for (const c of combat.combatants) (c.waiting ? waiting : ordered).push(c);
    return { ordered, waiting };
}

/** A row's element id, so the list can scroll the turn into view. */
export const combatantRowId = (combatantId: string) =>
    `combatant-${combatantId}`;

/** Whose turn it is, or undefined (no turn, or a hidden combatant's for a player). */
export const turnCombatant = (
    combat: Pick<Combat, "combatants" | "turnCombatantId">
): Combatant | undefined =>
    combat.turnCombatantId
        ? combat.combatants.find((c) => sameId(c.id, combat.turnCombatantId))
        : undefined;

// ── HP ───────────────────────────────────────────────────────────────────────

/** The API's band (18, Notes): Down at 0 or less, Bloodied at half or less. */
export function hpBand(
    hp: number,
    maxHp: number | null | undefined
): HpBand | null {
    if (maxHp == null) return null;
    if (hp <= 0) return "Down";
    return hp <= Math.floor(maxHp / 2) ? "Bloodied" : "Healthy";
}

export type HpView =
    | {
          kind: "exact";
          hp: number;
          maxHp: number | null;
          percent: number | null;
          band: HpBand | null;
      }
    | { kind: "band"; band: HpBand }
    | { kind: "none" };

/**
 * What a row shows of a combatant's HP. The server has already redacted it: a value
 * that arrives is one the viewer may read. `31 / 45` with a bar when HP is there, a
 * band chip when only the band is, and nothing otherwise.
 */
export function hpView(c: Pick<Combatant, "hp" | "maxHp" | "band">): HpView {
    if (c.hp != null) {
        const maxHp = c.maxHp ?? null;
        const percent = maxHp
            ? Math.max(0, Math.min(100, Math.round((c.hp / maxHp) * 100)))
            : null;
        return {
            kind: "exact",
            hp: c.hp,
            maxHp,
            percent,
            band: hpBand(c.hp, maxHp),
        };
    }
    if (c.band) return { kind: "band", band: c.band };
    return { kind: "none" };
}

/** What a DM's hidden row says players see (18c.4). */
export const PLAYERS_SEE_LABELS = {
    Exact: "HP and AC",
    Band: "Healthy / Bloodied / Down",
    Nothing: "No HP",
} as const;

// ── Labels ───────────────────────────────────────────────────────────────────

/** `Draft`, `Round 3` or `Finished`. */
export function combatStatusLabel(
    combat: Pick<Combat, "status" | "round">
): string {
    if (combat.status === "Draft") return "Draft";
    if (combat.status === "Finished") return "Finished";
    return `Round ${combat.round}`;
}

export const combatantCountLabel = (count: number) =>
    count === 0
        ? "No combatants"
        : count === 1
          ? "1 combatant"
          : `${count} combatants`;

/** A list row's second line: "Round 3 · 6 combatants", "3 rounds · 6 combatants". */
export function combatSummaryLine(
    summary: Pick<CombatSummary, "status" | "round" | "combatantCount">
): string {
    const count = combatantCountLabel(summary.combatantCount);
    if (summary.status === "Active") return `Round ${summary.round} · ${count}`;
    if (summary.status === "Finished" && summary.round > 0)
        return `${summary.round === 1 ? "1 round" : `${summary.round} rounds`} · ${count}`;
    if (summary.status === "Finished") return `Never started · ${count}`;
    return count;
}

/** "Goblin 2's turn". */
export const possessive = (name: string) => `${name}'s`;

// ── What each role can do (18c.4) ────────────────────────────────────────────

export type EndTurnState = {
    enabled: boolean;
    label: string;
    /** The viewer owns the combatant whose turn it is. */
    yourTurn: boolean;
    combatantId: string | null;
};

/** The End turn bar, or null when the combat is not running. */
export function endTurnState(
    combat: Combat,
    viewer: EntryViewer
): EndTurnState | null {
    if (combat.status !== "Active") return null;
    const turn = turnCombatant(combat);
    if (!turn)
        return {
            enabled: false,
            label: "Waiting for the turn",
            yourTurn: false,
            combatantId: null,
        };
    const yourTurn = sameId(turn.ownerMemberId, viewer.memberId);
    const label = yourTurn
        ? "End your turn"
        : viewer.isDm
          ? `End ${possessive(turn.name)} turn`
          : `${possessive(turn.name)} turn`;
    return {
        enabled: yourTurn || viewer.isDm,
        label,
        yourTurn,
        combatantId: turn.id,
    };
}

/** The DM's Roll button: "Start combat" on a Draft, "Roll 2 waiting" later, or none. */
export function rollLabel(
    combat: Pick<Combat, "status" | "combatants">
): string | null {
    if (combat.status === "Draft") return "Start combat";
    if (combat.status !== "Active") return null;
    const waiting = combat.combatants.filter((c) => c.waiting).length;
    return waiting > 0 ? `Roll ${waiting} waiting` : null;
}

export type CombatActions = {
    /** A DM's Add (the `@Goblin ×4` dialog). */
    add: boolean;
    /** A DM's Roll, labelled; null when there is nothing to roll. */
    roll: string | null;
    /** A DM's Finish (or discard, on a Draft). */
    finish: boolean;
    /** A player's Add my character. */
    addMyCharacter: boolean;
    /** A player's Roll my initiative, while their combatant waits. */
    rollMine: boolean;
    endTurn: EndTurnState | null;
};

export function combatActions(
    combat: Combat,
    viewer: EntryViewer
): CombatActions {
    const open = combat.status !== "Finished";
    const active = combat.status === "Active";
    return {
        add: viewer.isDm && open,
        roll: viewer.isDm ? rollLabel(combat) : null,
        finish: viewer.isDm && open,
        addMyCharacter: !viewer.isDm && active,
        rollMine:
            !viewer.isDm &&
            active &&
            combat.combatants.some(
                (c) => c.waiting && sameId(c.ownerMemberId, viewer.memberId)
            ),
        endTurn: endTurnState(combat, viewer),
    };
}

/** The viewer's claimed Character entries that are not in the combat yet (Add my character). */
export function myCharacterCandidates(
    directory: EntryDirectory,
    combat: Pick<Combat, "combatants">,
    memberId: string
): EntrySummary[] {
    const inCombat = new Set(
        combat.combatants.flatMap((c) => {
            if (!c.entryId) return [];
            const entry = resolveEntry(directory, c.entryId);
            return [(entry?.id ?? c.entryId).toLowerCase()];
        })
    );
    return directory.items
        .map((i) => i.entry)
        .filter(
            (e) =>
                e.kind === "Character" &&
                sameId(e.claimedByMemberId, memberId) &&
                !inCombat.has(e.id.toLowerCase())
        )
        .sort((a, b) =>
            a.name.localeCompare(b.name, undefined, { sensitivity: "base" })
        );
}

// ── The Combat tab's list ────────────────────────────────────────────────────

const newest =
    (at: (s: CombatSummary) => string | null | undefined) =>
    (a: CombatSummary, b: CombatSummary) =>
        (at(b) ?? b.createdAt).localeCompare(at(a) ?? a.createdAt);

/** Live, Drafts (DMs only) and Finished, newest first (18c.3). */
export function groupCombats(
    summaries: readonly CombatSummary[],
    isDm: boolean
): {
    live: CombatSummary[];
    drafts: CombatSummary[];
    finished: CombatSummary[];
} {
    const of = (status: CombatStatus) =>
        summaries.filter((s) => s.status === status);
    return {
        live: of("Active").sort(newest((s) => s.startedAt)),
        drafts: isDm ? of("Draft").sort(newest((s) => s.createdAt)) : [],
        finished: of("Finished").sort(newest((s) => s.finishedAt)),
    };
}

/**
 * A push's or a write's summary into a loaded list: replaced by id, or added, newest
 * first. A list read with a status filter drops a summary whose status it does not want.
 */
export function upsertCombatSummary(
    list: CombatList | undefined,
    summary: CombatSummary,
    statuses?: readonly CombatStatus[]
): CombatList | undefined {
    if (!list) return list;
    const others = list.combats.filter((c) => !sameId(c.id, summary.id));
    const wanted =
        !statuses || statuses.length === 0 || statuses.includes(summary.status);
    const combats = wanted ? [...others, summary] : others;
    return {
        ...list,
        combats: combats.sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
    };
}

/**
 * A write's response as a list summary, from the one already listed: the response has
 * no session number. Null when there is none to build on, or the combat moved session
 * (its first roll), and the caller reads the list again.
 */
export function summaryFromCombat(
    combat: Combat,
    previous: CombatSummary | undefined
): CombatSummary | null {
    if (!previous || previous.sessionId !== combat.sessionId) return null;
    return {
        ...previous,
        name: combat.name,
        status: combat.status,
        round: combat.round,
        combatantCount: combat.combatants.length,
        createdAt: combat.createdAt,
        startedAt: combat.startedAt,
        finishedAt: combat.finishedAt,
    };
}
