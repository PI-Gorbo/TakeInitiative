// The on-screen keyboard's height (14e, design §3a). 13d set
// `interactive-widget=overlays-content`, and iOS always overlays, so the keyboard
// covers the page instead of resizing it. `useKeyboardInset` feeds these rules from
// `visualViewport`; the composer is then pinned `inset` pixels above the bottom.

export type ViewportMetrics = {
    /** `window.innerHeight`: the layout viewport. */
    innerHeight: number;
    /** `visualViewport.height`, `offsetTop` and `scale`; undefined where the API is missing. */
    visual?: { height: number; offsetTop: number; scale: number };
};

/**
 * How far the visible area's bottom sits above the layout viewport's bottom: the
 * keyboard's height when it overlays the page. 0 without `visualViewport`, and 0 while
 * pinch-zoomed, where the visual viewport shrinks for another reason.
 */
export function keyboardInset({ innerHeight, visual }: ViewportMetrics): number {
    if (!visual || Math.abs(visual.scale - 1) > 0.01) return 0;
    const inset = innerHeight - (visual.height + visual.offsetTop);
    return inset > 0 ? Math.round(inset) : 0;
}

/**
 * Whether the composer is pinned above the keyboard: while its text box has focus on
 * a phone, or on any screen where a keyboard is covering the page (a tablet).
 */
export function composerPinned(args: { focused: boolean; phone: boolean; inset: number }): boolean {
    return args.focused && (args.phone || args.inset > 0);
}
