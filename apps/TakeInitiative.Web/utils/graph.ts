// The graph page (19d, glossary "Graph"): its URL state, how the API's nodes and edges
// become simulation input, and the drawing rules (node size, edge width, colours,
// which labels show). Pure, so `ConnectionGraph.vue` only wires them to d3 and the DOM.
import type {
    EntryConnection,
    EntryKind,
    GraphEdge,
    GraphNode,
} from "./api/types";
import { ENTRY_KINDS } from "./entries";

// ── URL state ────────────────────────────────────────────────────────────────

export const GRAPH_FOCUS_PARAM = "focus";
export const GRAPH_DEPTH_PARAM = "depth";
/** `?kinds=character,place`: the lower-case spelling of the Wiki's `?kind=`. */
export const GRAPH_KINDS_PARAM = "kinds";

export type GraphDepth = 1 | 2;
export type GraphView = {
    /** The entry the graph is drawn around, or null for the whole campaign. */
    focus: string | null;
    /** How far from the focus (ignored without one). */
    depth: GraphDepth;
    /** The kinds whose chips are on, in `ENTRY_KINDS` order. All of them by default. */
    kinds: EntryKind[];
};

const KIND_VALUES: readonly EntryKind[] = ENTRY_KINDS.map((k) => k.value);

/** Whether every kind is on (then the API is not asked to filter). */
export const allKinds = (kinds: readonly EntryKind[]) =>
    KIND_VALUES.every((k) => kinds.includes(k));

const first = (value: unknown): string | undefined => {
    const raw = Array.isArray(value) ? value[0] : value;
    return typeof raw === "string" ? raw : undefined;
};

/** The view a URL asks for. Anything unknown falls back to the default. */
export function graphViewFromQuery(query: Record<string, unknown>): GraphView {
    const focus = first(query[GRAPH_FOCUS_PARAM])?.trim() || null;
    const depth: GraphDepth = first(query[GRAPH_DEPTH_PARAM]) === "2" ? 2 : 1;
    const asked = (first(query[GRAPH_KINDS_PARAM]) ?? "")
        .split(",")
        .map((s) => s.trim().toLowerCase())
        .filter(Boolean);
    const kinds = KIND_VALUES.filter((k) => asked.includes(k.toLowerCase()));
    return { focus, depth, kinds: kinds.length > 0 ? kinds : [...KIND_VALUES] };
}

/**
 * The URL parameters for a view, each undefined when it is the default, so the plain
 * `/wiki/graph` is the whole campaign with every kind.
 */
export function graphViewToQuery(
    view: GraphView
): Record<
    | typeof GRAPH_FOCUS_PARAM
    | typeof GRAPH_DEPTH_PARAM
    | typeof GRAPH_KINDS_PARAM,
    string | undefined
> {
    return {
        [GRAPH_FOCUS_PARAM]: view.focus ?? undefined,
        [GRAPH_DEPTH_PARAM]: view.focus && view.depth === 2 ? "2" : undefined,
        [GRAPH_KINDS_PARAM]: allKinds(view.kinds)
            ? undefined
            : KIND_VALUES.filter((k) => view.kinds.includes(k))
                  .map((k) => k.toLowerCase())
                  .join(","),
    };
}

/**
 * A kind chip pressed: on becomes off and off becomes on, in `ENTRY_KINDS` order. The
 * last chip that is on stays on, so the graph is never empty by filter.
 */
export function toggleKind(
    kinds: readonly EntryKind[],
    kind: EntryKind
): EntryKind[] {
    if (kinds.includes(kind)) {
        return kinds.length === 1
            ? [...kinds]
            : kinds.filter((k) => k !== kind);
    }
    return KIND_VALUES.filter((k) => k === kind || kinds.includes(k));
}

/** The route to the graph around an entry (`[graph ↗]`). */
export const graphHref = (campaignId: string, focus?: string | null) => ({
    path: `/app/campaigns/${encodeURIComponent(campaignId)}/wiki/graph`,
    query: focus ? { [GRAPH_FOCUS_PARAM]: focus } : {},
});

// ── Drawing rules ────────────────────────────────────────────────────────────

/** An edge's stroke width: `1 + log2(weight)`, so 1 → 1px, 2 → 2px, 8 → 4px. */
export const edgeWidth = (weight: number) => 1 + Math.log2(Math.max(1, weight));

/** A node's radius: grows with the square root of its mention count, clamped. */
export const NODE_RADIUS_MIN = 12;
export const NODE_RADIUS_MAX = 30;
export const nodeRadius = (mentionCount: number) =>
    Math.min(
        NODE_RADIUS_MAX,
        NODE_RADIUS_MIN + 3 * Math.sqrt(Math.max(0, mentionCount))
    );

/** Heavier edges pull their ends closer. */
export const linkDistance = (weight: number) =>
    Math.max(50, 120 - 20 * Math.log2(Math.max(1, weight)));

/** Labels show for every node past this zoom. */
export const LABEL_ZOOM = 1.5;
/** A graph this small labels every node at any zoom. */
export const LABEL_ALL_MAX_NODES = 20;

/**
 * Whether a node's label shows: the focus (or, without one, the selected node), its
 * neighbours, and everything once zoomed in past 1.5×, or in a small graph.
 */
export function showLabel(
    nodeId: string,
    context: {
        anchorId: string | null;
        neighbours: ReadonlySet<string>;
        scale: number;
        nodeCount: number;
    }
): boolean {
    return (
        context.scale > LABEL_ZOOM ||
        context.nodeCount <= LABEL_ALL_MAX_NODES ||
        nodeId === context.anchorId ||
        context.neighbours.has(nodeId)
    );
}

/** An edge with any combat evidence ("Fought together") is dashed. */
export const edgeDashed = (edge: Pick<GraphEdge, "combats">) =>
    edge.combats > 0;

/**
 * One colour per kind, as HSL for SVG `fill` and `stroke`. The app has one dark theme
 * (`assets/index.css`), so these are bright enough to read on its background and are
 * never the gold the focus uses.
 */
export const KIND_COLOURS: Record<EntryKind, string> = {
    Character: "hsl(200 85% 62%)",
    Place: "hsl(145 55% 50%)",
    Faction: "hsl(0 75% 64%)",
    Item: "hsl(280 65% 70%)",
    Event: "hsl(20 90% 62%)",
    Other: "hsl(218 11% 65%)",
};

// ── Simulation input ─────────────────────────────────────────────────────────

/** A d3-force node: only an id and a size, never the API object Vue renders. */
export type SimNode = {
    id: string;
    radius: number;
    x?: number;
    y?: number;
    vx?: number;
    vy?: number;
    fx?: number | null;
    fy?: number | null;
    index?: number;
};
/** A d3-force link by ids. d3 swaps the ids for its nodes, so these are its own copies. */
export type SimLink = { source: string; target: string; weight: number };

/**
 * The simulation's input from a graph response: fresh objects keyed by entry id, so
 * d3's mutation never touches the response Vue renders. Edges to a node that is not
 * in the response are dropped. `previous` positions carry over, so filtering keeps the
 * nodes that stay where they were.
 */
export function toSimulation(
    nodes: readonly GraphNode[],
    edges: readonly GraphEdge[],
    previous?: ReadonlyMap<string, { x: number; y: number }>
): { nodes: SimNode[]; links: SimLink[] } {
    const ids = new Set(nodes.map((n) => n.entry.id));
    return {
        nodes: nodes.map((n) => {
            const at = previous?.get(n.entry.id);
            return {
                id: n.entry.id,
                radius: nodeRadius(n.mentionCount),
                ...(at ? { x: at.x, y: at.y } : {}),
            };
        }),
        links: edges
            .filter((e) => ids.has(e.a) && ids.has(e.b) && e.a !== e.b)
            .map((e) => ({ source: e.a, target: e.b, weight: e.weight })),
    };
}

/** The key of an undirected edge. */
export const edgeKey = (edge: Pick<GraphEdge, "a" | "b">) =>
    edge.a < edge.b ? `${edge.a}|${edge.b}` : `${edge.b}|${edge.a}`;

/** The ids connected to one node. */
export function neighboursOf(
    edges: readonly Pick<GraphEdge, "a" | "b">[],
    id: string | null
): Set<string> {
    const found = new Set<string>();
    if (!id) return found;
    for (const e of edges) {
        if (e.a === id) found.add(e.b);
        else if (e.b === id) found.add(e.a);
    }
    return found;
}

/** Each node's total weight: the sum of its edges' weights. */
export function totalWeights(
    edges: readonly Pick<GraphEdge, "a" | "b" | "weight">[]
): Map<string, number> {
    const totals = new Map<string, number>();
    for (const e of edges) {
        totals.set(e.a, (totals.get(e.a) ?? 0) + e.weight);
        totals.set(e.b, (totals.get(e.b) ?? 0) + e.weight);
    }
    return totals;
}

/**
 * The order nodes are drawn and tabbed through: heaviest total weight first, then by
 * name. The focus leads.
 */
export function nodeOrder(
    nodes: readonly GraphNode[],
    edges: readonly GraphEdge[],
    focusId: string | null
): GraphNode[] {
    const totals = totalWeights(edges);
    const w = (n: GraphNode) => totals.get(n.entry.id) ?? 0;
    return [...nodes].sort(
        (a, b) =>
            Number(b.entry.id === focusId) - Number(a.entry.id === focusId) ||
            w(b) - w(a) ||
            a.entry.name.localeCompare(b.entry.name, undefined, {
                sensitivity: "base",
            })
    );
}

/**
 * An edge as the evidence sheet's `connection`: seen from `from`, the other end with
 * the edge's weight split. The graph has no dates, so `lastAt` is null.
 */
export function edgeConnection(
    edge: GraphEdge,
    other: GraphNode
): EntryConnection {
    return {
        entry: other.entry,
        weight: edge.weight,
        notes: edge.notes,
        blocks: edge.blocks,
        combats: edge.combats,
        lastAt: null,
    };
}

/** "Gundren · Character · 7 mentions · 4 connections", for a node's bar and label. */
export function nodeSummary(
    node: GraphNode,
    connections: number,
    weight: number
): string {
    const kind =
        ENTRY_KINDS.find((k) => k.value === node.entry.kind)?.label ??
        node.entry.kind;
    const plural = (n: number, word: string) =>
        `${n} ${word}${n === 1 ? "" : "s"}`;
    return [
        kind,
        plural(node.mentionCount, "mention"),
        plural(connections, "connection"),
        `weight ${weight}`,
    ].join(" · ");
}
