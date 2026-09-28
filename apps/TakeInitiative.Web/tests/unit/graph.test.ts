import { describe, expect, it } from "vitest";
import type { EntryKind, GraphEdge, GraphNode } from "~/utils/api/types";
import {
    LABEL_ALL_MAX_NODES,
    NODE_RADIUS_MAX,
    NODE_RADIUS_MIN,
    edgeConnection,
    edgeDashed,
    edgeKey,
    edgeWidth,
    graphHref,
    graphViewFromQuery,
    graphViewToQuery,
    neighboursOf,
    nodeOrder,
    nodeRadius,
    showLabel,
    toSimulation,
    toggleKind,
    totalWeights,
} from "~/utils/graph";

const ALL: EntryKind[] = [
    "Character",
    "Place",
    "Faction",
    "Item",
    "Event",
    "Other",
];

function node(
    id: string,
    name: string,
    kind: EntryKind = "Character",
    mentionCount = 1
): GraphNode {
    return {
        entry: {
            id,
            name,
            kind,
            aliases: [],
            visibility: "Everyone",
            editAccess: "Anyone",
            creatorMemberId: "m1",
            createdAt: "2026-09-01T00:00:00Z",
            updatedAt: "2026-09-01T00:00:00Z",
            claimedByMemberId: null,
            mergedFromIds: [],
        },
        mentionCount,
        depth: null,
    };
}
const edge = (
    a: string,
    b: string,
    weight: number,
    combats = 0
): GraphEdge => ({
    a,
    b,
    weight,
    notes: weight - combats,
    blocks: 0,
    combats,
});

describe("edgeWidth", () => {
    it("is 1 + log2(weight)", () => {
        expect(edgeWidth(1)).toBe(1);
        expect(edgeWidth(2)).toBe(2);
        expect(edgeWidth(8)).toBe(4);
    });
    it("never drops below 1", () => {
        expect(edgeWidth(0)).toBe(1);
    });
    it("dashes an edge with combat evidence", () => {
        expect(edgeDashed(edge("a", "b", 3, 1))).toBe(true);
        expect(edgeDashed(edge("a", "b", 3))).toBe(false);
    });
});

describe("nodeRadius", () => {
    it("grows with the square root of mentions, clamped", () => {
        expect(nodeRadius(0)).toBe(NODE_RADIUS_MIN);
        expect(nodeRadius(4)).toBe(NODE_RADIUS_MIN + 6);
        expect(nodeRadius(10_000)).toBe(NODE_RADIUS_MAX);
    });
});

describe("showLabel", () => {
    const big = LABEL_ALL_MAX_NODES + 1;
    const context = {
        anchorId: "f",
        neighbours: new Set(["n"]),
        scale: 1,
        nodeCount: big,
    };
    it("labels the focus and its neighbours", () => {
        expect(showLabel("f", context)).toBe(true);
        expect(showLabel("n", context)).toBe(true);
        expect(showLabel("x", context)).toBe(false);
    });
    it("labels everything past 1.5×", () => {
        expect(showLabel("x", { ...context, scale: 1.5 })).toBe(false);
        expect(showLabel("x", { ...context, scale: 1.6 })).toBe(true);
    });
    it("labels everything in a small graph", () => {
        expect(
            showLabel("x", { ...context, nodeCount: LABEL_ALL_MAX_NODES })
        ).toBe(true);
    });
    it("labels nothing extra without an anchor", () => {
        expect(
            showLabel("x", {
                ...context,
                anchorId: null,
                neighbours: new Set(),
            })
        ).toBe(false);
    });
});

describe("URL state", () => {
    it("defaults to the whole campaign, every kind, depth 1", () => {
        expect(graphViewFromQuery({})).toEqual({
            focus: null,
            depth: 1,
            kinds: ALL,
        });
        expect(graphViewToQuery(graphViewFromQuery({}))).toEqual({
            focus: undefined,
            depth: undefined,
            kinds: undefined,
        });
    });

    it("round-trips a focus, depth 2 and some kinds", () => {
        const query = { focus: "e1", depth: "2", kinds: "place,character" };
        const view = graphViewFromQuery(query);
        expect(view).toEqual({
            focus: "e1",
            depth: 2,
            kinds: ["Character", "Place"],
        });
        expect(graphViewToQuery(view)).toEqual({
            focus: "e1",
            depth: "2",
            kinds: "character,place",
        });
    });

    it("reads kinds in any case and ignores unknown ones", () => {
        expect(graphViewFromQuery({ kinds: "PLACE,dragon" }).kinds).toEqual([
            "Place",
        ]);
        expect(graphViewFromQuery({ kinds: "dragon" }).kinds).toEqual(ALL);
    });

    it("drops depth without a focus, and a bad depth is 1", () => {
        expect(graphViewToQuery({ focus: null, depth: 2, kinds: ALL })).toEqual(
            { focus: undefined, depth: undefined, kinds: undefined }
        );
        expect(graphViewFromQuery({ focus: "e1", depth: "7" }).depth).toBe(1);
    });

    it("takes the first of a repeated parameter", () => {
        expect(graphViewFromQuery({ focus: ["e1", "e2"] }).focus).toBe("e1");
    });

    it("links to the graph around an entry", () => {
        expect(graphHref("c 1", "e1")).toEqual({
            path: "/app/campaigns/c%201/wiki/graph",
            query: { focus: "e1" },
        });
        expect(graphHref("c1").query).toEqual({});
    });
});

describe("toggleKind", () => {
    it("turns a kind off and on, in kind order", () => {
        const off = toggleKind(ALL, "Place");
        expect(off).not.toContain("Place");
        expect(toggleKind(off, "Place")).toEqual(ALL);
        expect(toggleKind(["Item"], "Character")).toEqual([
            "Character",
            "Item",
        ]);
    });
    it("keeps the last kind on", () => {
        expect(toggleKind(["Item"], "Item")).toEqual(["Item"]);
    });
});

describe("toSimulation", () => {
    const nodes = [node("a", "Gundren", "Character", 9), node("b", "Tharden")];
    const edges = [edge("a", "b", 3), edge("a", "gone", 1)];

    it("gives d3 ids, not the API's objects", () => {
        const sim = toSimulation(nodes, edges);
        expect(sim.nodes).toEqual([
            { id: "a", radius: nodeRadius(9) },
            { id: "b", radius: nodeRadius(1) },
        ]);
        expect(sim.links).toEqual([{ source: "a", target: "b", weight: 3 }]);
        // Nothing d3 mutates is shared with what Vue renders.
        for (const n of sim.nodes) expect(nodes).not.toContain(n);
        for (const l of sim.links) expect(edges).not.toContain(l);
    });

    it("drops edges to missing nodes and self-edges", () => {
        const sim = toSimulation(nodes, [...edges, edge("a", "a", 2)]);
        expect(sim.links).toHaveLength(1);
    });

    it("keeps the positions of nodes that stay", () => {
        const sim = toSimulation(
            nodes,
            edges,
            new Map([["a", { x: 5, y: -3 }]])
        );
        expect(sim.nodes[0]).toMatchObject({ id: "a", x: 5, y: -3 });
        expect(sim.nodes[1]).not.toHaveProperty("x");
    });
});

describe("graph helpers", () => {
    const nodes = [
        node("a", "Gundren"),
        node("b", "Tharden"),
        node("c", "Phandalin", "Place"),
        node("d", "Anvil"),
    ];
    const edges = [edge("a", "b", 3), edge("a", "c", 1), edge("b", "c", 5)];

    it("keys an edge the same both ways", () => {
        expect(edgeKey(edge("a", "b", 1))).toBe(edgeKey(edge("b", "a", 1)));
    });

    it("finds neighbours and total weights", () => {
        expect([...neighboursOf(edges, "a")].sort()).toEqual(["b", "c"]);
        expect(neighboursOf(edges, null).size).toBe(0);
        expect(totalWeights(edges).get("c")).toBe(6);
    });

    it("orders nodes focus first, then heaviest, then by name", () => {
        expect(nodeOrder(nodes, edges, null).map((n) => n.entry.id)).toEqual([
            "b",
            "c",
            "a",
            "d",
        ]);
        expect(nodeOrder(nodes, edges, "a")[0]!.entry.id).toBe("a");
    });

    it("turns an edge into the evidence sheet's connection", () => {
        expect(edgeConnection(edge("a", "b", 3, 1), nodes[1]!)).toEqual({
            entry: nodes[1]!.entry,
            weight: 3,
            notes: 2,
            blocks: 0,
            combats: 1,
            lastAt: null,
        });
    });
});
