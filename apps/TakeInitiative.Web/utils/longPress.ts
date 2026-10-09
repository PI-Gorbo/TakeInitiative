// Long-press on a note (14e, design §3a): 500ms without moving opens the action sheet.
// `useLongPress` runs the timer; these are its rules.

/** How long a finger must rest on a note. */
export const LONG_PRESS_MS = 500;
/** How far it may drift, in CSS pixels, before the press counts as a scroll. */
export const LONG_PRESS_TOLERANCE = 10;

export type Point = { x: number; y: number };

/** Movement past the tolerance cancels the press (the reader is scrolling). */
export function movedTooFar(start: Point, now: Point, tolerance = LONG_PRESS_TOLERANCE): boolean {
    return Math.hypot(now.x - start.x, now.y - start.y) > tolerance;
}

/**
 * Whether a press may start a long-press. Only touch and pen (a mouse has the ⋯ menu
 * and a right click), only the primary pointer, and never on a control inside the
 * note, where a long-press means something else (a link, the editor, a button).
 */
export function canStartLongPress(args: {
    pointerType: string;
    isPrimary: boolean;
    onControl: boolean;
    disabled: boolean;
}): boolean {
    return (
        !args.disabled && args.isPrimary && !args.onControl && (args.pointerType === "touch" || args.pointerType === "pen")
    );
}

/** The elements a long-press leaves alone. */
export const LONG_PRESS_IGNORE = "a, button, input, textarea, select, [contenteditable], [role=menuitem]";
