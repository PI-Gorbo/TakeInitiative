// Combat cards (18e): a combat drawn in its session in the session stream, and in an
// entry's COMBATS. Pure, with type-only imports, like `sessionStreamCache.ts`.
import type { Combat, CombatCard, CombatCardCombatant, SessionNote } from "./api/types";

const COPY_NUMBER = /\s+\d+$/;

/**
 * The card of a combat, from the viewer's own view of it: the same grouping as the API's
 * `CombatCard.From`, so a `combatChanged` push updates a card the way a read would draw it.
 * Combatants of one entry become one line (`4× @Goblin`), named after the first without its
 * copy number; plain names are listed as they are, in the combat's order.
 */
export function cardFromCombat(combat: Combat): CombatCard {
    const rows: CombatCardCombatant[] = [];
    const byEntry = new Map<string, number>();
    for (const c of combat.combatants) {
        const entryId = c.entryId ?? null;
        const at = entryId ? byEntry.get(entryId.toLowerCase()) : undefined;
        if (at !== undefined) {
            const row = rows[at];
            rows[at] = { ...row, count: row.count + 1, name: row.name.replace(COPY_NUMBER, "") };
            continue;
        }
        if (entryId) byEntry.set(entryId.toLowerCase(), rows.length);
        rows.push(entryId ? { name: c.name, entryId, count: 1 } : { name: c.name, count: 1 });
    }
    return {
        id: combat.id,
        sessionId: combat.sessionId,
        name: combat.name,
        status: combat.status,
        round: combat.round,
        createdAt: combat.createdAt,
        startedAt: combat.startedAt ?? null,
        finishedAt: combat.finishedAt ?? null,
        combatants: rows,
    };
}

/** When a card sits in its session: when the combat started, or was created for a Draft. */
export const cardTime = (card: Pick<CombatCard, "startedAt" | "createdAt">) =>
    Date.parse(card.startedAt ?? card.createdAt);

/** A session's notes and combat cards, merged by time. */
export type StreamItem = { kind: "note"; note: SessionNote } | { kind: "combat"; card: CombatCard };

/**
 * Merges a session's notes (by `postedAt`) and cards (by `startedAt ?? createdAt`) into one
 * list, oldest first. Both inputs are already in order; a note and a card at the same time
 * keep the note first.
 */
export function mergeStreamItems(notes: readonly SessionNote[], cards: readonly CombatCard[]): StreamItem[] {
    const out: StreamItem[] = [];
    let n = 0;
    let c = 0;
    while (n < notes.length || c < cards.length) {
        const takeNote =
            c >= cards.length || (n < notes.length && Date.parse(notes[n].postedAt) <= cardTime(cards[c]));
        if (takeNote) out.push({ kind: "note", note: notes[n++] });
        else out.push({ kind: "combat", card: cards[c++] });
    }
    return out;
}

/** The card's state: "Draft", "Round 3" while live, "3 rounds" once finished, or "Never started". */
export function cardRoundsLabel(card: Pick<CombatCard, "status" | "round">): string {
    if (card.status === "Draft") return "Draft";
    if (card.status === "Active") return `Round ${card.round}`;
    if (card.round === 0) return "Never started";
    return card.round === 1 ? "1 round" : `${card.round} rounds`;
}

/** "4× " before a line of several combatants of one entry. */
export const cardCountPrefix = (row: Pick<CombatCardCombatant, "count">) => (row.count > 1 ? `${row.count}× ` : "");

/** The live combat the banner names (the newest), and how many more are live. */
export function liveCombatBanner<T extends { status: string; startedAt?: string | null; createdAt: string }>(
    combats: readonly T[] | undefined
): { combat: T; more: number } | null {
    const live = (combats ?? []).filter((c) => c.status === "Active");
    if (live.length === 0) return null;
    const newest = live.reduce((a, b) => (cardTime(b) > cardTime(a) ? b : a));
    return { combat: newest, more: live.length - 1 };
}
