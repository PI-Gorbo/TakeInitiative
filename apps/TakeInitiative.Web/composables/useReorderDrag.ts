import type { MaybeRefOrGetter, Ref } from "vue";
import { dropIndexAt } from "~/utils/combat";
import { movedTooFar, type Point } from "~/utils/longPress";

/** A touch rests this long on a handle before the row lifts (18d.6), so a swipe still scrolls. */
export const REORDER_TOUCH_MS = 250;
/** How close to the scroller's edge, in CSS pixels, a drag scrolls it. */
const EDGE = 64;
/** The most it scrolls per frame. */
const SPEED = 12;

function scrollerOf(el: HTMLElement): HTMLElement {
    for (let p = el.parentElement; p; p = p.parentElement) {
        const { overflowY } = getComputedStyle(p);
        if (
            (overflowY === "auto" || overflowY === "scroll") &&
            p.scrollHeight > p.clientHeight
        )
            return p;
    }
    return (document.scrollingElement as HTMLElement) ?? document.body;
}

/**
 * Drag to reorder one list (18d.6), with pointer events and no dependency. Rows carry
 * `data-reorder-id`; a handle calls `start(event, id)` on `pointerdown`. A mouse or pen
 * lifts the row at once, a touch after resting `REORDER_TOUCH_MS` without moving (moving
 * first is a scroll, which is left alone). While a row is lifted the page does not
 * scroll under the finger, the scroller follows the pointer near its edges, and Escape
 * or a cancelled pointer drops nothing. `onDrop` gets the index among the other rows.
 */
export function useReorderDrag(options: {
    list: Ref<HTMLElement | null | undefined>;
    enabled: MaybeRefOrGetter<boolean>;
    onDrop: (draggedId: string, index: number) => void;
}) {
    /** The lifted row's id, once the drag has started. */
    const draggingId = ref<string | null>(null);
    /** Where it would land, among the other rows. */
    const dropIndex = ref<number | null>(null);
    /** How far the lifted row has moved from where it was, in CSS pixels. */
    const offsetY = ref(0);

    let pendingId: string | null = null;
    let pointerId: number | null = null;
    let origin: Point = { x: 0, y: 0 };
    let lastY = 0;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let scroller: HTMLElement | null = null;
    let startScroll = 0;
    let frame = 0;
    let stops: (() => void)[] = [];

    const rows = () =>
        Array.from(
            options.list.value?.querySelectorAll<HTMLElement>(
                "[data-reorder-id]"
            ) ?? []
        );

    function measure() {
        if (!draggingId.value) return;
        const scrolled = (scroller?.scrollTop ?? 0) - startScroll;
        offsetY.value = lastY - origin.y + scrolled;
        const middles = rows()
            .filter((r) => r.dataset.reorderId !== draggingId.value)
            .map((r) => {
                const rect = r.getBoundingClientRect();
                return rect.top + rect.height / 2;
            });
        dropIndex.value = dropIndexAt(middles, lastY);
    }

    function autoScroll() {
        frame = 0;
        if (!draggingId.value || !scroller) return;
        const isPage = scroller === document.scrollingElement;
        const top = isPage ? 0 : scroller.getBoundingClientRect().top;
        const bottom = isPage
            ? window.innerHeight
            : scroller.getBoundingClientRect().bottom;
        let by = 0;
        if (lastY < top + EDGE)
            by = -Math.ceil(((top + EDGE - lastY) / EDGE) * SPEED);
        else if (lastY > bottom - EDGE)
            by = Math.ceil(((lastY - (bottom - EDGE)) / EDGE) * SPEED);
        if (by !== 0) {
            scroller.scrollTop += by;
            measure();
        }
        frame = requestAnimationFrame(autoScroll);
    }

    function lift(id: string) {
        const row = rows().find((r) => r.dataset.reorderId === id);
        if (!row) return end(false);
        scroller = scrollerOf(row);
        startScroll = scroller.scrollTop;
        draggingId.value = id;
        navigator.vibrate?.(10);
        measure();
        frame = requestAnimationFrame(autoScroll);
    }

    function end(drop: boolean) {
        clearTimeout(timer);
        timer = undefined;
        cancelAnimationFrame(frame);
        frame = 0;
        const id = draggingId.value;
        const index = dropIndex.value;
        draggingId.value = null;
        dropIndex.value = null;
        offsetY.value = 0;
        pendingId = null;
        pointerId = null;
        scroller = null;
        for (const stop of stops) stop();
        stops = [];
        if (drop && id && index !== null) options.onDrop(id, index);
    }

    function listen<K extends keyof WindowEventMap>(
        type: K,
        handler: (event: WindowEventMap[K]) => void,
        opts?: AddEventListenerOptions
    ) {
        window.addEventListener(type, handler, opts);
        stops.push(() => window.removeEventListener(type, handler, opts));
    }

    function start(event: PointerEvent, id: string) {
        if (!toValue(options.enabled) || !event.isPrimary) return;
        if (event.pointerType === "mouse" && event.button !== 0) return;
        if (pendingId || draggingId.value) end(false);
        pendingId = id;
        pointerId = event.pointerId;
        origin = { x: event.clientX, y: event.clientY };
        lastY = event.clientY;

        listen("pointermove", (e) => {
            if (e.pointerId !== pointerId) return;
            lastY = e.clientY;
            if (draggingId.value) measure();
            else if (movedTooFar(origin, { x: e.clientX, y: e.clientY }))
                end(false); // a scroll, not a drag
        });
        listen("pointerup", (e) => {
            if (e.pointerId === pointerId) end(true);
        });
        listen("pointercancel", (e) => {
            if (e.pointerId === pointerId) end(false);
        });
        listen("keydown", (e) => {
            if (e.key === "Escape" && draggingId.value) {
                e.preventDefault();
                end(false);
            }
        });
        // Once lifted, the finger drags the row, not the page. Before that, the page
        // scrolls as usual, and the browser's long-press menu stays away.
        listen(
            "touchmove",
            (e) => {
                if (draggingId.value && e.cancelable) e.preventDefault();
            },
            { passive: false }
        );
        listen("contextmenu", (e) => e.preventDefault());

        if (event.pointerType === "touch") {
            timer = setTimeout(() => {
                timer = undefined;
                if (pendingId === id) lift(id);
            }, REORDER_TOUCH_MS);
        } else {
            event.preventDefault(); // no text selection while a mouse drags
            lift(id);
        }
    }

    onBeforeUnmount(() => end(false));

    return {
        draggingId: readonly(draggingId),
        dropIndex: readonly(dropIndex),
        offsetY: readonly(offsetY),
        start,
    };
}
