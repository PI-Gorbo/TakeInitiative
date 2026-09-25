import { useEventListener, useMediaQuery } from "@vueuse/core";
import type { Ref } from "vue";
import type { EntryKind } from "~/utils/api/types";
import { ENTRY_KINDS } from "~/utils/entries";
import {
    activeMention,
    createEntry,
    insertMentionTrigger,
    mentionSuggestions,
    orphanedLinkIds,
    pickEntry,
    type MentionEdit,
    type MentionSuggestion,
    type MentionText,
} from "~/utils/mentions";
import { useEntryDirectory } from "~/utils/queries/entries";
import type { RefOrGetter } from "~/utils/queries/utils";

/**
 * The `@` picker (15d, design §3 and §3a) for any `<textarea>` whose text is a
 * `MentionText`: the session composer, the note editor, and 15f's article editor.
 * The rules are in `utils/mentions.ts`; this wires them to the caret and the keys.
 * Draw the result with `<ComposerMentionStrip v-bind="picker.stripProps.value" …>`.
 *
 * - Desktop (from md): a popover at the caret. ↑/↓ move, Enter or Tab picks, and on a
 *   Create row Tab cycles the kind and Shift+Tab goes back. Esc dismisses.
 * - Phone: the mention strip, which the caller places above the keyboard (the
 *   composer's strip slot, or `docked` for an editor that is not pinned). Tapping
 *   Create shows the kind chips; tapping a kind creates the entry.
 */
export function useMentionPicker(args: {
    /** Reactive; the picker writes `text`, `links` and `newEntries`. */
    state: MentionText;
    textarea: Readonly<Ref<HTMLTextAreaElement | null>>;
    campaignId: RefOrGetter<string>;
    /** False while something else owns the keys (the `/` strip). */
    enabled?: () => boolean;
}) {
    const { state, textarea } = args;
    const directory = useEntryDirectory(args.campaignId);
    const desktop = useMediaQuery("(min-width: 768px)");
    const touch = useMediaQuery("(pointer: coarse)");

    // ── The caret ─────────────────────────────────────────────────────────────
    // `selectionStart` is not reactive, so it is read on every event that moves it.
    const caret = ref<number | null>(null);
    const focused = ref(false);
    let blurTimer: ReturnType<typeof setTimeout> | undefined;
    const readCaret = () => {
        const el = textarea.value;
        caret.value = el && el.selectionStart === el.selectionEnd ? el.selectionStart : null;
    };
    for (const event of ["input", "keyup", "click", "select", "focus"] as const) {
        useEventListener(textarea, event, readCaret);
    }
    useEventListener(textarea, "focus", () => {
        clearTimeout(blurTimer);
        focused.value = true;
    });
    // A grace period, so a tap on the strip (which keeps focus anyway) does not flicker.
    useEventListener(textarea, "blur", () => {
        clearTimeout(blurTimer);
        blurTimer = setTimeout(() => (focused.value = false), 150);
    });
    if (import.meta.client) {
        useEventListener(document, "selectionchange", () => {
            if (document.activeElement === textarea.value) readCaret();
        });
    }
    onBeforeUnmount(() => clearTimeout(blurTimer));

    // ── The query and its suggestions ────────────────────────────────────────
    const active = computed(() =>
        caret.value === null || !focused.value || (args.enabled && !args.enabled())
            ? null
            : activeMention(state.text, caret.value, state.links)
    );

    // Esc closes the query until another `@` starts.
    const dismissedAt = ref<number | null>(null);
    watch(active, (next) => {
        if (!next || next.start !== dismissedAt.value) dismissedAt.value = null;
    });

    const suggestions = computed<MentionSuggestion[]>(() => {
        const current = active.value;
        if (!current || current.start === dismissedAt.value) return [];
        // In a bracket query, the entries this text linked before its text was
        // edited come first: pick, override the text, pick again.
        const prefer = current.kind === "bracket" ? orphanedLinkIds(state) : [];
        return mentionSuggestions(current.query, directory.value, state.newEntries, prefer);
    });
    const open = computed(() => suggestions.value.length > 0);

    const highlighted = ref(0);
    const kind = ref<EntryKind>("Character");
    watch(
        () => active.value?.query,
        () => {
            highlighted.value = 0;
            kind.value = "Character";
        }
    );
    watch(suggestions, (list) => {
        if (highlighted.value >= list.length) highlighted.value = 0;
    });
    const creating = computed(() => suggestions.value[highlighted.value]?.kind === "create");

    // ── Picking ───────────────────────────────────────────────────────────────
    function apply(edit: MentionEdit) {
        state.text = edit.text;
        state.links = edit.links;
        state.newEntries = edit.newEntries;
        caret.value = edit.caret;
        void nextTick(() => {
            const el = textarea.value;
            if (!el) return;
            el.focus();
            el.setSelectionRange(edit.caret, edit.caret);
            readCaret();
        });
    }

    function pick(index: number, withKind: EntryKind = kind.value) {
        const current = active.value;
        const suggestion = suggestions.value[index];
        if (!current || !suggestion) return;
        if (suggestion.kind === "create") {
            apply(createEntry(state, current, withKind, crypto.randomUUID()));
        } else {
            apply(pickEntry(state, current, { id: suggestion.entryId, name: suggestion.name }));
        }
    }

    /**
     * A click or tap on a suggestion. On a phone, the first tap on Create chooses it
     * and shows the kind chips; the next tap (or a kind chip) creates.
     */
    function choose(index: number) {
        const suggestion = suggestions.value[index];
        if (suggestion?.kind === "create" && !desktop.value && highlighted.value !== index) {
            highlighted.value = index;
            return;
        }
        pick(index);
    }

    /** A kind chip: create the chosen Create row with that kind. */
    function createAs(entryKind: EntryKind) {
        kind.value = entryKind;
        const index = suggestions.value.findIndex((s) => s.kind === "create");
        if (index >= 0) pick(index, entryKind);
    }

    function cycleKind(step: 1 | -1) {
        const kinds = ENTRY_KINDS.map((k) => k.value);
        const at = kinds.indexOf(kind.value);
        kind.value = kinds[(at + step + kinds.length) % kinds.length]!;
    }

    /** The `@` toolbar button: insert `@` and open the strip. */
    function trigger() {
        const el = textarea.value;
        const next = insertMentionTrigger({
            text: state.text,
            selectionStart: el?.selectionStart ?? state.text.length,
            selectionEnd: el?.selectionEnd ?? state.text.length,
        });
        dismissedAt.value = null;
        state.text = next.text;
        focused.value = true;
        caret.value = next.selectionStart;
        void nextTick(() => {
            el?.focus();
            el?.setSelectionRange(next.selectionStart, next.selectionEnd);
            readCaret();
        });
    }

    /** Handles a key for the picker; true when it did. Enter belongs to the text on a touch screen. */
    function onKeydown(event: KeyboardEvent): boolean {
        const list = suggestions.value;
        if (list.length === 0 || event.isComposing) return false;
        switch (event.key) {
            case "ArrowDown":
                highlighted.value = (highlighted.value + 1) % list.length;
                break;
            case "ArrowUp":
                highlighted.value = (highlighted.value - 1 + list.length) % list.length;
                break;
            case "Tab":
                if (creating.value) cycleKind(event.shiftKey ? -1 : 1);
                else if (event.shiftKey) return false;
                else pick(highlighted.value);
                break;
            case "Enter":
                if (event.shiftKey || touch.value) return false;
                pick(highlighted.value);
                break;
            case "Escape":
                dismissedAt.value = active.value?.start ?? null;
                break;
            default:
                return false;
        }
        event.preventDefault();
        return true;
    }

    // ── The popover's place (desktop) ────────────────────────────────────────
    /** Where the query starts, relative to the textarea's box: the popover sits above it. */
    const anchor = computed(() => {
        const el = textarea.value;
        const current = active.value;
        if (!el || !current || !open.value || !desktop.value) return null;
        const point = caretPoint(el, current.start);
        const lineHeight = Number.parseFloat(getComputedStyle(el).lineHeight) || 20;
        return { left: point.left, top: point.top - el.scrollTop, lineHeight };
    });

    return {
        directory,
        active,
        suggestions,
        open,
        highlighted,
        kind,
        creating,
        anchor,
        pick,
        choose,
        createAs,
        cycleKind,
        trigger,
        onKeydown,
    };
}

export type MentionPicker = ReturnType<typeof useMentionPicker>;

// The styles that decide where text wraps, copied onto a hidden mirror of the
// textarea so the caret's position can be measured.
const MIRRORED = [
    "boxSizing",
    "width",
    "borderTopWidth",
    "borderRightWidth",
    "borderBottomWidth",
    "borderLeftWidth",
    "paddingTop",
    "paddingRight",
    "paddingBottom",
    "paddingLeft",
    "fontFamily",
    "fontSize",
    "fontStyle",
    "fontWeight",
    "letterSpacing",
    "lineHeight",
    "tabSize",
    "textIndent",
    "textTransform",
    "wordSpacing",
] as const;

/** The top-left of the character at `position`, in pixels from the textarea's border box. */
function caretPoint(el: HTMLTextAreaElement, position: number): { left: number; top: number } {
    const mirror = document.createElement("div");
    const style = getComputedStyle(el);
    for (const key of MIRRORED) mirror.style[key] = style[key];
    mirror.style.position = "absolute";
    mirror.style.visibility = "hidden";
    mirror.style.whiteSpace = "pre-wrap";
    mirror.style.overflowWrap = "break-word";
    mirror.style.top = "0";
    mirror.style.left = "-9999px";
    mirror.textContent = el.value.slice(0, position);
    const marker = document.createElement("span");
    marker.textContent = el.value.slice(position) || ".";
    mirror.appendChild(marker);
    document.body.appendChild(mirror);
    const point = { left: marker.offsetLeft, top: marker.offsetTop };
    mirror.remove();
    return point;
}
