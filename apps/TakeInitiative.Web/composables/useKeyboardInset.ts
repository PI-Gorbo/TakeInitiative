import { keyboardInset } from "~/utils/keyboardInset";

/**
 * The on-screen keyboard's height in CSS pixels (14e, design §3a), from
 * `visualViewport`. iOS fires `scroll` as well as `resize` while the keyboard
 * animates, so both are listened to. 0 where `visualViewport` is missing.
 */
export function useKeyboardInset() {
    const inset = ref(0);
    if (import.meta.server) return readonly(inset);

    const viewport = window.visualViewport;
    const measure = () => {
        inset.value = keyboardInset({
            innerHeight: window.innerHeight,
            visual: viewport
                ? { height: viewport.height, offsetTop: viewport.offsetTop, scale: viewport.scale }
                : undefined,
        });
    };

    if (viewport) {
        onMounted(() => {
            measure();
            viewport.addEventListener("resize", measure);
            viewport.addEventListener("scroll", measure);
            window.addEventListener("resize", measure);
        });
        onBeforeUnmount(() => {
            viewport.removeEventListener("resize", measure);
            viewport.removeEventListener("scroll", measure);
            window.removeEventListener("resize", measure);
        });
    }
    return readonly(inset);
}

/**
 * Whether the composer is pinned above the keyboard. The layout reads it to hide the
 * tab bar, so nothing sits between the composer and the keyboard (invariant 11).
 */
export const useComposerPinned = () => useState<boolean>("composerPinned", () => false);
