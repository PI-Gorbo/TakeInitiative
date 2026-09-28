<template>
    <!-- The graph (19d, glossary "Graph"): d3-force lays the nodes out and Vue draws them
         as SVG, so nodes and edges are ordinary elements with click and keyboard
         handlers. d3-zoom gives pan, wheel and pinch zoom. d3's node objects never enter
         Vue's reactivity: their positions are copied out once per animation frame. -->
    <div
        ref="container"
        class="relative size-full overflow-hidden">
        <svg
            ref="svg"
            class="block size-full touch-none select-none"
            role="group"
            :aria-label="ariaLabel"
            @click="onBackgroundClick">
            <g :transform="`translate(${view.x},${view.y}) scale(${view.k})`">
                <g>
                    <g
                        v-for="edge in edgeViews"
                        :key="edge.key">
                        <line
                            :x1="at(edge.a).x"
                            :y1="at(edge.a).y"
                            :x2="at(edge.b).x"
                            :y2="at(edge.b).y"
                            stroke="hsl(var(--muted-foreground))"
                            :stroke-opacity="edgeOpacity(edge)"
                            :stroke-width="edge.width"
                            :stroke-dasharray="edge.dashed ? '6 4' : undefined"
                            stroke-linecap="round"
                            pointer-events="none" />
                        <!-- A wider, invisible line a finger can land on. -->
                        <line
                            :x1="at(edge.a).x"
                            :y1="at(edge.a).y"
                            :x2="at(edge.b).x"
                            :y2="at(edge.b).y"
                            stroke="transparent"
                            stroke-width="12"
                            vector-effect="non-scaling-stroke"
                            pointer-events="stroke"
                            class="cursor-pointer"
                            @click.stop="emit('edge', edge.edge)">
                            <title>{{ edge.title }}</title>
                        </line>
                    </g>
                </g>
                <g>
                    <g
                        v-for="node in nodeViews"
                        :key="node.id"
                        data-graph-node
                        :transform="`translate(${at(node.id).x},${at(node.id).y})`"
                        role="button"
                        tabindex="0"
                        :aria-label="node.ariaLabel"
                        :aria-pressed="selected === node.id"
                        class="graph-node cursor-pointer outline-none"
                        @pointerdown="onNodePointerDown($event, node.id)"
                        @pointermove="onNodePointerMove"
                        @pointerup="onNodePointerUp"
                        @pointercancel="onNodePointerUp"
                        @click.stop="onNodeClick(node.id)"
                        @keydown.enter.prevent="toggleSelected(node.id)"
                        @keydown.space.prevent="toggleSelected(node.id)"
                        @focus="onNodeFocus(node.id)">
                        <!-- At least 44px across on screen, whatever the zoom. -->
                        <circle
                            :r="Math.max(node.radius, 22 / view.k)"
                            fill="transparent" />
                        <circle
                            class="graph-node-ring"
                            :r="node.radius + 4"
                            fill="none"
                            :stroke="
                                node.isFocus
                                    ? 'hsl(var(--gold))'
                                    : selected === node.id
                                      ? 'hsl(var(--foreground))'
                                      : 'transparent'
                            "
                            :stroke-width="node.isFocus ? 3 : 2" />
                        <circle
                            :r="node.radius"
                            :fill="node.colour"
                            fill-opacity="0.22"
                            :stroke="node.colour"
                            stroke-width="2" />
                        <text
                            text-anchor="middle"
                            dominant-baseline="central"
                            :font-size="node.radius * 0.9"
                            aria-hidden="true">
                            {{ node.icon }}
                        </text>
                        <text
                            v-if="labelShown(node.id)"
                            :y="node.radius + 13"
                            text-anchor="middle"
                            font-size="11"
                            class="fill-foreground"
                            stroke="hsl(var(--background))"
                            stroke-width="3"
                            paint-order="stroke"
                            aria-hidden="true">
                            {{ node.name }}
                        </text>
                    </g>
                </g>
            </g>
        </svg>
    </div>
</template>

<script setup lang="ts">
    import { usePreferredReducedMotion, useResizeObserver } from "@vueuse/core";
    import {
        forceCenter,
        forceCollide,
        forceLink,
        forceManyBody,
        forceSimulation,
        forceX,
        forceY,
        type Simulation,
    } from "d3-force";
    import { select } from "d3-selection";
    import { zoom, zoomIdentity, type ZoomBehavior } from "d3-zoom";
    import type { GraphEdge, GraphNode } from "~/utils/api/types";
    import { ENTRY_KIND_ICONS } from "~/utils/entries";
    import {
        KIND_COLOURS,
        edgeDashed,
        edgeKey,
        edgeWidth,
        linkDistance,
        neighboursOf,
        nodeOrder,
        nodeRadius,
        showLabel,
        toSimulation,
        totalWeights,
        type SimLink,
        type SimNode,
    } from "~/utils/graph";

    const props = defineProps<{
        nodes: readonly GraphNode[];
        edges: readonly GraphEdge[];
        focusId: string | null;
    }>();
    const selected = defineModel<string | null>("selected", {
        required: true,
    });
    const emit = defineEmits<{ edge: [edge: GraphEdge] }>();

    const container = ref<HTMLDivElement>();
    const svg = ref<SVGSVGElement>();
    const reducedMotion = usePreferredReducedMotion();

    // ── What is drawn ────────────────────────────────────────────────────────

    const byId = computed(
        () => new Map(props.nodes.map((n) => [n.entry.id, n]))
    );
    const nodeViews = computed(() => {
        const totals = totalWeights(props.edges);
        const degree = new Map<string, number>();
        for (const e of props.edges) {
            degree.set(e.a, (degree.get(e.a) ?? 0) + 1);
            degree.set(e.b, (degree.get(e.b) ?? 0) + 1);
        }
        return nodeOrder(props.nodes, props.edges, props.focusId).map((n) => {
            const count = degree.get(n.entry.id) ?? 0;
            return {
                id: n.entry.id,
                name: n.entry.name,
                icon: ENTRY_KIND_ICONS[n.entry.kind],
                colour: KIND_COLOURS[n.entry.kind],
                radius: nodeRadius(n.mentionCount),
                isFocus: n.entry.id === props.focusId,
                ariaLabel: `${n.entry.name}, ${n.entry.kind}, ${count} connection${count === 1 ? "" : "s"}, weight ${totals.get(n.entry.id) ?? 0}`,
            };
        });
    });
    const edgeViews = computed(() =>
        props.edges
            .filter((e) => byId.value.has(e.a) && byId.value.has(e.b))
            .map((e) => ({
                key: edgeKey(e),
                a: e.a,
                b: e.b,
                edge: e,
                width: edgeWidth(e.weight),
                dashed: edgeDashed(e),
                title: `${byId.value.get(e.a)!.entry.name} ↔ ${byId.value.get(e.b)!.entry.name} (${e.weight})${e.combats > 0 ? ", fought together" : ""}`,
            }))
    );
    const ariaLabel = computed(
        () =>
            `Connections graph: ${props.nodes.length} entries, ${edgeViews.value.length} connections`
    );

    // Labels: the focus (or the selected node) and its neighbours, or all when zoomed in.
    const anchorId = computed(() => props.focusId ?? selected.value);
    const neighbours = computed(() =>
        neighboursOf(props.edges, anchorId.value)
    );
    const labelShown = (id: string) =>
        showLabel(id, {
            anchorId: anchorId.value,
            neighbours: neighbours.value,
            scale: view.value.k,
            nodeCount: props.nodes.length,
        });
    // With a node selected, its edges stand out and the rest recede.
    const edgeOpacity = (edge: { a: string; b: string }) => {
        const s = selected.value;
        if (!s) return 0.45;
        return edge.a === s || edge.b === s ? 0.9 : 0.15;
    };

    // ── Positions: copied out of d3 once a frame ─────────────────────────────

    const positions = shallowRef(new Map<string, { x: number; y: number }>());
    const origin = { x: 0, y: 0 };
    const at = (id: string) => positions.value.get(id) ?? origin;

    let simulation: Simulation<SimNode, SimLink> | null = null;
    let simNodes = new Map<string, SimNode>();
    let frame = 0;

    function copyPositions() {
        const next = new Map<string, { x: number; y: number }>();
        for (const [id, n] of simNodes)
            next.set(id, { x: n.x ?? 0, y: n.y ?? 0 });
        positions.value = next;
    }
    function scheduleCopy() {
        if (frame) return;
        frame = requestAnimationFrame(() => {
            frame = 0;
            copyPositions();
        });
    }

    // The view fits the graph once it has settled, and again when the focus moves.
    let fitted = false;

    function build() {
        simulation?.stop();
        const previous = positions.value;
        const input = toSimulation(props.nodes, props.edges, previous);
        const nodes = input.nodes.map((n) => markRaw(n));
        simNodes = new Map(nodes.map((n) => [n.id, n]));
        const focus = props.focusId ? simNodes.get(props.focusId) : undefined;
        if (focus && focus.x === undefined) {
            focus.fx = 0;
            focus.fy = 0;
        }
        const known = nodes.filter((n) => previous.has(n.id)).length;

        simulation = markRaw(
            forceSimulation<SimNode, SimLink>(nodes)
                .force(
                    "link",
                    forceLink<SimNode, SimLink>(input.links)
                        .id((d) => d.id)
                        .distance((l) => linkDistance(l.weight))
                )
                .force(
                    "charge",
                    forceManyBody<SimNode>().strength(-240).distanceMax(700)
                )
                .force(
                    "collide",
                    forceCollide<SimNode>((d) => d.radius + 8)
                )
                .force("center", forceCenter(0, 0))
                // Keeps islands of entries from drifting off.
                .force("x", forceX<SimNode>(0).strength(0.04))
                .force("y", forceY<SimNode>(0).strength(0.04))
                .alphaMin(0.01)
        );
        // Mostly the same nodes (a kind chip, a refetch): a gentle reheat.
        if (known > 0 && known >= nodes.length / 2) simulation.alpha(0.3);

        if (reducedMotion.value === "reduce") {
            // Laid out up front and drawn once.
            simulation.stop();
            simulation.tick(300);
            copyPositions();
            if (!fitted) fit();
            fitted = true;
            return;
        }
        simulation.on("tick", scheduleCopy).on("end", () => {
            copyPositions();
            if (!fitted) {
                fitted = true;
                void nextTick(fit);
            }
        });
    }

    // ── Zoom ─────────────────────────────────────────────────────────────────

    const view = shallowRef({ x: 0, y: 0, k: 1 });
    let zoomBehavior: ZoomBehavior<SVGSVGElement, unknown> | null = null;
    const size = { width: 0, height: 0 };

    /** Zooms so the whole graph shows, no closer than 1.5×. */
    function fit() {
        if (!svg.value || !zoomBehavior || simNodes.size === 0) return;
        let minX = Infinity,
            minY = Infinity,
            maxX = -Infinity,
            maxY = -Infinity;
        for (const n of simNodes.values()) {
            const pad = n.radius + 24;
            minX = Math.min(minX, (n.x ?? 0) - pad);
            minY = Math.min(minY, (n.y ?? 0) - pad);
            maxX = Math.max(maxX, (n.x ?? 0) + pad);
            maxY = Math.max(maxY, (n.y ?? 0) + pad);
        }
        const k = Math.max(
            0.1,
            Math.min(
                1.5,
                size.width / (maxX - minX),
                size.height / (maxY - minY)
            )
        );
        zoomBehavior.transform(
            select(svg.value),
            zoomIdentity
                .translate(size.width / 2, size.height / 2)
                .scale(k)
                .translate(-(minX + maxX) / 2, -(minY + maxY) / 2)
        );
    }
    defineExpose({ fit });

    onMounted(() => {
        const el = svg.value!;
        const rect = el.getBoundingClientRect();
        size.width = rect.width;
        size.height = rect.height;
        zoomBehavior = zoom<SVGSVGElement, unknown>()
            .scaleExtent([0.1, 4])
            // A press on a node drags the node, not the view; a second finger still pinches.
            .filter((event: Event) => {
                if (event.type === "wheel") return true;
                if ((event as MouseEvent).button) return false;
                if (
                    event.type === "touchstart" &&
                    (event as TouchEvent).touches.length > 1
                )
                    return true;
                const target = event.target as Element | null;
                return !target?.closest?.("[data-graph-node]");
            })
            .on("zoom", (event) => {
                const t = event.transform;
                view.value = { x: t.x, y: t.y, k: t.k };
            });
        select(el).call(zoomBehavior).on("dblclick.zoom", null);
        zoomBehavior.transform(
            select(el),
            zoomIdentity.translate(size.width / 2, size.height / 2)
        );
        build();
    });

    useResizeObserver(container, (entries) => {
        const box = entries[0]?.contentRect;
        if (!box || !zoomBehavior || !svg.value) return;
        // Keep the centre where it was.
        const dx = (box.width - size.width) / 2;
        const dy = (box.height - size.height) / 2;
        size.width = box.width;
        size.height = box.height;
        if (dx || dy)
            zoomBehavior.translateBy(
                select(svg.value),
                dx / view.value.k,
                dy / view.value.k
            );
    });

    // A new focus fits again; declared first, so it runs before the rebuild.
    watch(
        () => props.focusId,
        () => {
            fitted = false;
        }
    );
    watch(
        () => [props.nodes, props.edges] as const,
        () => {
            if (zoomBehavior) build();
        }
    );

    onBeforeUnmount(() => {
        simulation?.stop();
        if (frame) cancelAnimationFrame(frame);
        if (svg.value) select(svg.value).on(".zoom", null);
    });

    // ── Nodes: tap to select, drag to move ──────────────────────────────────

    function toggleSelected(id: string) {
        selected.value = selected.value === id ? null : id;
    }
    function onBackgroundClick(event: MouseEvent) {
        if (!(event.target as Element).closest("[data-graph-node]"))
            selected.value = null;
    }

    let drag: {
        id: string;
        pointerId: number;
        x: number;
        y: number;
        moved: boolean;
    } | null = null;
    let suppressClick = false;

    function toGraph(event: PointerEvent) {
        const rect = svg.value!.getBoundingClientRect();
        const t = view.value;
        return {
            x: (event.clientX - rect.left - t.x) / t.k,
            y: (event.clientY - rect.top - t.y) / t.k,
        };
    }
    function onNodePointerDown(event: PointerEvent, id: string) {
        if (event.button !== 0) return;
        suppressClick = false;
        (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
        drag = {
            id,
            pointerId: event.pointerId,
            x: event.clientX,
            y: event.clientY,
            moved: false,
        };
    }
    function onNodePointerMove(event: PointerEvent) {
        if (!drag || event.pointerId !== drag.pointerId) return;
        if (
            !drag.moved &&
            Math.hypot(event.clientX - drag.x, event.clientY - drag.y) < 5
        )
            return;
        drag.moved = true;
        const node = simNodes.get(drag.id);
        if (!node || !simulation) return;
        const p = toGraph(event);
        node.fx = p.x;
        node.fy = p.y;
        if (reducedMotion.value === "reduce") {
            node.x = p.x;
            node.y = p.y;
            copyPositions();
        } else {
            // The simulation reheats while the node is held.
            simulation.alphaTarget(0.3).restart();
        }
    }
    function onNodePointerUp(event: PointerEvent) {
        if (!drag || event.pointerId !== drag.pointerId) return;
        const node = simNodes.get(drag.id);
        if (drag.moved) {
            suppressClick = true;
            simulation?.alphaTarget(0);
            // The focus stays where it is dropped; the rest rejoin the layout.
            if (node && node.id !== props.focusId) {
                node.fx = null;
                node.fy = null;
            }
        }
        drag = null;
    }
    function onNodeClick(id: string) {
        if (suppressClick) {
            suppressClick = false;
            return;
        }
        toggleSelected(id);
    }

    // Tabbing to a node off screen brings it into view.
    function onNodeFocus(id: string) {
        const p = positions.value.get(id);
        if (!p || !svg.value || !zoomBehavior) return;
        const t = view.value;
        const sx = p.x * t.k + t.x;
        const sy = p.y * t.k + t.y;
        const margin = 40;
        if (
            sx < margin ||
            sy < margin ||
            sx > size.width - margin ||
            sy > size.height - margin
        )
            zoomBehavior.translateTo(select(svg.value), p.x, p.y);
    }
</script>

<style scoped>
    .graph-node:focus-visible .graph-node-ring {
        stroke: hsl(var(--ring));
        stroke-width: 3;
    }
</style>
