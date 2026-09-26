import type { Ref } from "vue";
import type { Session } from "~/utils/api/types";
import type { ComposerState } from "~/utils/composer";
import {
    applyCommands,
    commandSuggestions,
    pickSuggestion,
    type CommandChange,
    type CommandSuggestion,
} from "~/utils/composerCommands";

/**
 * The composer's `/` commands (14e). Watches the text: a complete command at its start
 * is consumed into the composer's state and removed from the text. While a command is
 * being typed, `suggestions` lists what the strip offers; `onKeydown` lets the arrow
 * keys, Tab and Enter pick from it on desktop.
 */
export function useComposerCommands(args: {
    state: ComposerState;
    sessions: Readonly<Ref<Session[]>>;
    textarea: Readonly<Ref<HTMLTextAreaElement | null>>;
}) {
    const { state, sessions, textarea } = args;

    function apply(change: CommandChange) {
        if (change.isRecap !== undefined) state.isRecap = change.isRecap;
        if (change.visibility !== undefined) state.visibility = change.visibility;
        if (change.sessionId !== undefined) state.sessionId = change.sessionId;
    }

    function setText(text: string, caret: number) {
        state.text = text;
        void nextTick(() => {
            const el = textarea.value;
            if (el && document.activeElement === el) el.setSelectionRange(caret, caret);
        });
    }

    // Consume complete commands as they are typed (or pasted).
    watch(
        () => state.text,
        (text) => {
            if (!text.startsWith("/")) return;
            const result = applyCommands(text, sessions.value);
            if (result.removed === 0) return;
            apply(result.change);
            const caret = textarea.value?.selectionStart ?? text.length;
            setText(result.text, Math.max(0, caret - result.removed));
        },
        { flush: "sync" }
    );

    /** "There is no Session 9." while that command is still at the start. */
    const error = computed(() => (state.text.startsWith("/") ? applyCommands(state.text, sessions.value).error : null));

    // The strip. Escape dismisses it until the text changes.
    const dismissedFor = ref<string | null>(null);
    const suggestions = computed<CommandSuggestion[]>(() =>
        dismissedFor.value === state.text ? [] : commandSuggestions(state.text, sessions.value)
    );
    const active = ref(0);
    watch(suggestions, () => (active.value = 0));

    function pick(suggestion: CommandSuggestion) {
        const next = pickSuggestion(state.text, suggestion);
        apply(next.change);
        setText(next.text, next.text.length);
        textarea.value?.focus();
    }

    /** Handles a key for the strip; true when it did. */
    function onKeydown(event: KeyboardEvent): boolean {
        const list = suggestions.value;
        if (list.length === 0 || event.isComposing) return false;
        switch (event.key) {
            case "ArrowDown":
                active.value = (active.value + 1) % list.length;
                break;
            case "ArrowUp":
                active.value = (active.value - 1 + list.length) % list.length;
                break;
            case "Tab":
            case "Enter":
                if (event.shiftKey) return false;
                pick(list[active.value]!);
                break;
            case "Escape":
                dismissedFor.value = state.text;
                break;
            default:
                return false;
        }
        event.preventDefault();
        return true;
    }

    return { suggestions, active, error, pick, onKeydown };
}
