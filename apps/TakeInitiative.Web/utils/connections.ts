import type {
    EntryConnection,
    EntryKind,
    NoteEvidence,
} from "~/utils/api/types";

// The Connections panel (19c, design §6). The API answers an entry's connections with
// their weights; this groups them for the panel, with "Seen at" as a heading, not a
// relation (19, Notes "'Seen at' is a grouping, not a query").

/** How many chips show before "+ n more". */
export const CONNECTIONS_SHOWN = 12;

export type ConnectionGroupKey = "seenAt" | "seenHere" | "all";
export type ConnectionGroup = {
    key: ConnectionGroupKey;
    /** The group's heading, or null for the one plain group. */
    label: string | null;
    connections: EntryConnection[];
};

/**
 * Heaviest first, then the most recent evidence (blocks only, with no date, last), then
 * by name. The API sends this order already; sorting again keeps the panel stable
 * whatever arrives.
 */
export function sortConnections(
    connections: readonly EntryConnection[]
): EntryConnection[] {
    const time = (c: EntryConnection) =>
        c.lastAt ? Date.parse(c.lastAt) : Number.NEGATIVE_INFINITY;
    return [...connections].sort(
        (a, b) =>
            b.weight - a.weight ||
            time(b) - time(a) ||
            a.entry.name.localeCompare(b.entry.name, undefined, {
                sensitivity: "base",
            })
    );
}

/** The kind a "Seen at" group collects on an entry of this kind, and its heading. */
const SEEN_GROUPS: Partial<
    Record<
        EntryKind,
        { kind: EntryKind; key: ConnectionGroupKey; label: string }
    >
> = {
    Character: { kind: "Place", key: "seenAt", label: "Seen at" },
    Place: { kind: "Character", key: "seenHere", label: "Seen here" },
};

/**
 * The panel's groups, in order. On a Character its Places come first under "Seen at";
 * on a Place its Characters under "Seen here". Everything else is one plain group,
 * headed "Also connected" only when it follows a "Seen" group. Empty groups are left
 * out, and each group is heaviest first.
 */
export function groupConnections(
    kind: EntryKind,
    connections: readonly EntryConnection[]
): ConnectionGroup[] {
    const sorted = sortConnections(connections);
    const seen = SEEN_GROUPS[kind];
    if (!seen)
        return sorted.length > 0
            ? [{ key: "all", label: null, connections: sorted }]
            : [];
    const inSeen = sorted.filter((c) => c.entry.kind === seen.kind);
    const rest = sorted.filter((c) => c.entry.kind !== seen.kind);
    const groups: ConnectionGroup[] = [];
    if (inSeen.length > 0)
        groups.push({ key: seen.key, label: seen.label, connections: inSeen });
    if (rest.length > 0)
        groups.push({
            key: "all",
            label: inSeen.length > 0 ? "Also connected" : null,
            connections: rest,
        });
    return groups;
}

/**
 * The first `limit` chips in the panel's order (groups first to last), and how many
 * "+ n more" hides. A group left with no chips is dropped.
 */
export function limitConnectionGroups(
    groups: readonly ConnectionGroup[],
    limit = CONNECTIONS_SHOWN
): { groups: ConnectionGroup[]; hidden: number } {
    const total = groups.reduce((n, g) => n + g.connections.length, 0);
    let left = Math.max(0, limit);
    const shown: ConnectionGroup[] = [];
    for (const group of groups) {
        if (left === 0) break;
        const connections = group.connections.slice(0, left);
        left -= connections.length;
        shown.push({ ...group, connections });
    }
    return { groups: shown, hidden: total - (Math.max(0, limit) - left) };
}

/** "Fought together" (glossary): the pair were both visible combatants in a started combat. */
export const foughtTogether = (connection: Pick<EntryConnection, "combats">) =>
    connection.combats > 0;

/** "3 pieces of evidence" (the chip's accessible count). */
export const evidenceCountLabel = (count: number) =>
    `${count} ${count === 1 ? "piece" : "pieces"} of evidence`;

/** The chip's accessible name: "Tharden, 3 pieces of evidence, fought together". */
export function connectionAriaLabel(connection: EntryConnection): string {
    const parts = [
        connection.entry.name,
        evidenceCountLabel(connection.weight),
    ];
    if (foughtTogether(connection)) parts.push("fought together");
    return parts.join(", ");
}

/** A note row's "S14 · Sam". */
export const noteEvidenceLabel = (
    note: Pick<NoteEvidence, "sessionNumber" | "authorMemberId">,
    nameOf: (memberId: string) => string
) => `S${note.sessionNumber} · ${nameOf(note.authorMemberId)}`;

/** The empty panel's line. */
export const noConnectionsLabel = (name: string) =>
    `No connections yet. Mention another entry in a note about ${name}.`;
