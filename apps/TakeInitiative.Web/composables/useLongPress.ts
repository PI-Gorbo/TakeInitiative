import { useEventListener } from "@vueuse/core";
import type { MaybeRefOrGetter } from "vue";
import {
    LONG_PRESS_IGNORE,
    LONG_PRESS_MS,
    canStartLongPress,
    movedTooFar,
    type Point,
} from "~/utils/longPress";

/**
 * Calls `onLongPress` when a finger rests on `target` for 500ms without moving
 * (14e). Movement, lifting the finger or the browser taking over the gesture cancels
 * it. After a long-press the system's own long-press menu (text selection, the
 * callout) and the click that follows are suppressed.
 */
export function useLongPress(
    target: MaybeRefOrGetter<HTMLElement | null | undefined>,
    onLongPress: () => void,
    options: { disabled?: MaybeRefOrGetter<boolean> } = {}
) {
    let timer: ReturnType<typeof setTimeout> | undefined;
    let start: Point | null = null;
    let fired = false;

    const cancel = () => {
        clearTimeout(timer);
        timer = undefined;
        start = null;
    };

    useEventListener(target, "pointerdown", (event: PointerEvent) => {
        fired = false;
        const onControl = event.target instanceof Element && !!event.target.closest(LONG_PRESS_IGNORE);
        if (
            !canStartLongPress({
                pointerType: event.pointerType,
                isPrimary: event.isPrimary,
                onControl,
                disabled: !!toValue(options.disabled),
            })
        )
            return;
        start = { x: event.clientX, y: event.clientY };
        clearTimeout(timer);
        timer = setTimeout(() => {
            timer = undefined;
            start = null;
            fired = true;
            navigator.vibrate?.(10);
            onLongPress();
        }, LONG_PRESS_MS);
    });

    useEventListener(
        target,
        "pointermove",
        (event: PointerEvent) => {
            if (start && movedTooFar(start, { x: event.clientX, y: event.clientY })) cancel();
        },
        { passive: true }
    );
    useEventListener(target, ["pointerup", "pointercancel", "pointerleave"], cancel);

    // The browser's own long-press menu, and the click that ends a long-press.
    useEventListener(target, "contextmenu", (event: Event) => {
        if (fired || timer) event.preventDefault();
    });
    useEventListener(
        target,
        "click",
        (event: Event) => {
            if (!fired) return;
            fired = false;
            event.preventDefault();
            event.stopPropagation();
        },
        { capture: true }
    );

    onBeforeUnmount(cancel);
}
