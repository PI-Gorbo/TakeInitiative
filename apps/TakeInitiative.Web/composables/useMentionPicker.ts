import type { Editor, Range } from "@tiptap/core";
import type { SuggestionOptions } from "@tiptap/suggestion";
import { exitSuggestion } from "@tiptap/suggestion";
import { PluginKey } from "@tiptap/pm/state";
import { useEventListener, useMediaQuery } from "@vueuse/core";
import type { Ref, ShallowRef } from "vue";
import type { EntryKind } from "~/utils/api/types";
import { ENTRY_KINDS } from "~/utils/entries";
import { insertMention, insertMentionTrigger as insertEditorTrigger, mentionQuery } from "~/utils/editorExtensions";
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
    type NewEntry,
} from "~/utils/mentions";
import { useEntryDirectory } from "~/utils/queries/entries";
import type { RefOrGetter } from "~/utils/queries/utils";

/** The query the picker answers: where it starts (for Esc), its text, and ids to put first. */
type PickerQuery = { start: number; query: string; prefer?: readonly string[] };
/** Where the desktop popover goes: the query's start, relative to the box the strip is placed in. */
type PickerAnchor = { left: number; top: number; lineHeight: number };

/**
 * The `@` picker's rules, whatever the text box (15d, design §3 and §3a): the
 * suggestions for a query, the highlighted row, the kind a Create makes, and the keys.
 * `useMentionPicker` feeds it from a `<textarea>`, `useEditorMentionPicker` from the
 * composer's TipTap editor (step 17, the rich text box); both draw with `<ComposerMentionStrip>`.
 *
 * - Desktop (from md): a popover at the caret. ↑/↓ move, Enter or Tab picks, and on a
 *   Create row Tab cycles the kind and Shift+Tab goes back. Esc dismisses.
 * - Phone: the mention strip, which the caller places above the keyboard (the
 *   composer's strip slot, or `docked` for an editor that is not pinned). Tapping
 *   Create shows the kind chips; tapping a kind creates the entry.
 */
function useMentionSuggestions(args: {
    campaignId: RefOrGetter<string>;
    newEntries: () => readonly NewEntry[];
    /** The open query, or null; the front end folds focus and "enabled" into it. */
    query: () => PickerQuery | null;
    /** Link the query to a suggestion (a Create makes a new entry of `kind`). */
    commit: (suggestion: MentionSuggestion, kind: EntryKind) => void;
    /** Esc: close the query until another `@` starts. */
    dismiss: () => void;
    /** The `@` toolbar button. */
    trigger: () => void;
    anchor: () => PickerAnchor | null;
}) {
    const directory = useEntryDirectory(args.campaignId);
    const desktop = useMediaQuery("(min-width: 768px)");
    const touch = useMediaQuery("(pointer: coarse)");

    const suggestions = computed<MentionSuggestion[]>(() => {
        const current = args.query();
        if (!current) return [];
        return mentionSuggestions(current.query, directory.value, args.newEntries(), current.prefer ?? []);
    });
    const open = computed(() => suggestions.value.length > 0);

    const highlighted = ref(0);
    const kind = ref<EntryKind>("Character");
    watch(
        () => args.query()?.query,
        () => {
            highlighted.value = 0;
            kind.value = "Character";
        }
    );
    watch(suggestions, (list) => {
        if (highlighted.value >= list.length) highlighted.value = 0;
    });
    const creating = computed(() => suggestions.value[highlighted.value]?.kind === "create");

    function pick(index: number, withKind: EntryKind = kind.value) {
        const suggestion = suggestions.value[index];
        if (suggestion) args.commit(suggestion, withKind);
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
                args.dismiss();
                break;
            default:
                return false;
        }
        event.preventDefault();
        return true;
    }

    const anchor = computed(() => (open.value && desktop.value ? args.anchor() : null));

    return {
        directory,
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
        trigger: args.trigger,
        onKeydown,
    };
}

/**
 * The `@` picker for a `<textarea>` whose text is a `MentionText`: 15f's article
 * editor. The rules are in `utils/mentions.ts`; this wires them to the
 * caret. Draw the result with `<ComposerMentionStrip :picker="picker" …>`.
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

    // ── The query ────────────────────────────────────────────────────────────
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

    const picker = useMentionSuggestions({
        campaignId: args.campaignId,
        newEntries: () => state.newEntries,
        query: () => {
            const current = active.value;
            if (!current || current.start === dismissedAt.value) return null;
            // In a bracket query, the entries this text linked before its text was
            // edited come first: pick, override the text, pick again.
            const prefer = current.kind === "bracket" ? orphanedLinkIds(state) : [];
            return { start: current.start, query: current.query, prefer };
        },
        commit: (suggestion, kind) => {
            const current = active.value;
            if (!current) return;
            if (suggestion.kind === "create") apply(createEntry(state, current, kind, crypto.randomUUID()));
            else apply(pickEntry(state, current, { id: suggestion.entryId, name: suggestion.name }));
        },
        dismiss: () => (dismissedAt.value = active.value?.start ?? null),
        trigger,
        // Where the query starts, relative to the textarea's box: the popover sits above it.
        anchor: () => {
            const el = textarea.value;
            const current = active.value;
            if (!el || !current) return null;
            const point = caretPoint(el, current.start);
            const lineHeight = Number.parseFloat(getComputedStyle(el).lineHeight) || 20;
            return { left: point.left, top: point.top - el.scrollTop, lineHeight };
        },
    });
    return { ...picker, active };
}

/** The suggestion plugin's view of an open `@` query in the editor. */
type EditorQuery = { range: Range; query: string; clientRect: (() => DOMRect | null) | null };

/**
 * The `@` picker for the composer's TipTap editor (step 17, the rich text box). TipTap's suggestion
 * plugin finds the query (an `@` at the start of a line or after a space or `(`, up to
 * the caret); this feeds it to the same rules and strip as the textarea's picker, and
 * a pick inserts a mention node. `suggestion` goes to the Mention extension's
 * `suggestion` option; `onFocus` and `onBlur` come from the editor.
 *
 * What the textarea's bracket query did (re-link `@[…]` after editing its text) has no
 * place here: a mention is one atomic node, so its text cannot be edited apart from it.
 */
export function useEditorMentionPicker(args: {
    editor: Readonly<ShallowRef<Editor | undefined>>;
    /** The positioned box the strip is drawn in: the desktop popover is placed in it. */
    box: Readonly<Ref<HTMLElement | null>>;
    campaignId: RefOrGetter<string>;
    newEntries: () => readonly NewEntry[];
    /** A Create: the new entry, made when the note is posted (15b). */
    addNewEntry: (entry: NewEntry) => void;
    /** False while something else owns the keys (the `/` strip). */
    enabled?: () => boolean;
}) {
    const pluginKey = new PluginKey("entryMention");
    const current = shallowRef<EditorQuery | null>(null);
    const focused = ref(false);
    let blurTimer: ReturnType<typeof setTimeout> | undefined;
    onBeforeUnmount(() => clearTimeout(blurTimer));

    const query = computed<PickerQuery | null>(() => {
        const open = current.value;
        if (!open || !focused.value || (args.enabled && !args.enabled())) return null;
        const text = mentionQuery(open.query);
        return text === null ? null : { start: open.range.from, query: text };
    });

    function insert(entry: { id: string; name: string; isNew: boolean }) {
        const editor = args.editor.value;
        const open = current.value;
        if (editor && open) insertMention(editor, open.range, entry);
    }

    function trigger() {
        if (args.editor.value) insertEditorTrigger(args.editor.value);
    }

    const picker = useMentionSuggestions({
        campaignId: args.campaignId,
        newEntries: args.newEntries,
        query: () => query.value,
        commit: (suggestion, kind) => {
            if (suggestion.kind === "entry") {
                insert({ id: suggestion.entryId, name: suggestion.name, isNew: suggestion.isNew });
                return;
            }
            const entry: NewEntry = { id: crypto.randomUUID(), name: suggestion.name, kind };
            args.addNewEntry(entry);
            insert({ ...entry, isNew: true });
        },
        dismiss: () => {
            const view = args.editor.value?.view;
            if (view) exitSuggestion(view, pluginKey);
        },
        trigger,
        anchor: () => {
            const rect = current.value?.clientRect?.();
            const box = args.box.value?.getBoundingClientRect();
            if (!rect || !box) return null;
            return { left: rect.left - box.left, top: rect.top - box.top, lineHeight: rect.height || 20 };
        },
    });

    const suggestion: Omit<SuggestionOptions, "editor"> = {
        char: "@",
        pluginKey,
        // Names have spaces: "Gundren Rock". The rules close the query when it ends in
        // a space and matches nothing.
        allowSpaces: true,
        allowedPrefixes: [" ", "(", "\u00a0", "\t"],
        // The picker draws its own rows; the plugin only tracks the query.
        items: () => [],
        render: () => ({
            onStart: (props) => (current.value = { range: props.range, query: props.query, clientRect: props.clientRect ?? null }),
            onUpdate: (props) => (current.value = { range: props.range, query: props.query, clientRect: props.clientRect ?? null }),
            onExit: () => (current.value = null),
        }),
    };

    return {
        picker,
        suggestion,
        onFocus() {
            clearTimeout(blurTimer);
            focused.value = true;
        },
        // A grace period, so a tap on the strip (which keeps focus anyway) does not flicker.
        onBlur() {
            clearTimeout(blurTimer);
            blurTimer = setTimeout(() => (focused.value = false), 150);
        },
    };
}

export type MentionPicker = ReturnType<typeof useMentionSuggestions>;

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
