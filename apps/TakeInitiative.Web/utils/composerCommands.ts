// The composer's `/` commands (14e, design §3). A command counts only at the start of
// the text, and is consumed into the composer's state as soon as a space follows it,
// so it never reaches the note's text. Every command also has a touch control:
// /recap is the 📜 Recap toggle, /dm and /me the visibility picker, /session N the
// session picker. The composable `useComposerCommands` wires these rules to the DOM.
import type { Session } from "./api/types";
import type { ComposerState } from "./composer";

export type ComposerCommandName = "recap" | "dm" | "me" | "session";

export type ComposerCommand = {
    name: ComposerCommandName;
    /** What the strip shows, e.g. "/session N". */
    usage: string;
    hint: string;
};

export const COMPOSER_COMMANDS: readonly ComposerCommand[] = [
    { name: "recap", usage: "/recap", hint: "Mark as a recap" },
    { name: "dm", usage: "/dm", hint: "Visible to the DMs" },
    { name: "me", usage: "/me", hint: "Visible only to you" },
    { name: "session", usage: "/session N", hint: "Post to Session N" },
];

/** The state a command changes. */
export type CommandChange = Partial<Pick<ComposerState, "isRecap" | "visibility" | "sessionId">>;

export type CommandResult =
    /** No complete command at the start of the text. */
    | { kind: "none" }
    /** A command was consumed: `text` is what is left, `removed` how many characters went. */
    | { kind: "applied"; command: ComposerCommandName; change: CommandChange; text: string; removed: number }
    /** `/session N ` for a session that does not exist. The text is left alone. */
    | { kind: "error"; message: string };

// A complete command: the name, an argument for /session, then whitespace.
const SIMPLE = /^\/(recap|dm|me)\s/i;
const SESSION = /^\/session\s+(\S+)\s/i;

/** The change a picked session makes: the current one is stored as "nothing picked". */
export const sessionChange = (session: Session): CommandChange => ({
    sessionId: session.isCurrent ? null : session.id,
});

/** Recognises one complete command at the very start of the text. */
export function parseCommand(text: string, sessions: readonly Session[]): CommandResult {
    const simple = SIMPLE.exec(text);
    if (simple) {
        const name = simple[1]!.toLowerCase() as "recap" | "dm" | "me";
        const change: CommandChange =
            name === "recap" ? { isRecap: true } : { visibility: name === "dm" ? "DM" : "Me" };
        return consume(text, simple[0].length, name, change);
    }
    const session = SESSION.exec(text);
    if (session) {
        const arg = session[1]!;
        const number = /^\d+$/.test(arg) ? Number(arg) : NaN;
        const found = sessions.find((s) => s.number === number);
        if (!found) {
            return {
                kind: "error",
                message: Number.isNaN(number)
                    ? `"/session" needs a session number, like /session ${currentNumber(sessions)}.`
                    : `There is no Session ${number}.`,
            };
        }
        return consume(text, session[0].length, "session", sessionChange(found));
    }
    return { kind: "none" };
}

function consume(text: string, length: number, command: ComposerCommandName, change: CommandChange): CommandResult {
    return { kind: "applied", command, change, text: text.slice(length), removed: length };
}

const currentNumber = (sessions: readonly Session[]) => sessions.find((s) => s.isCurrent)?.number ?? 1;

/**
 * Applies every complete command at the start of the text, one after another, so a
 * pasted "/dm /recap We left…" sets both. Stops at the first error, leaving that
 * command's text alone.
 */
export function applyCommands(
    text: string,
    sessions: readonly Session[]
): { text: string; change: CommandChange; removed: number; error: string | null } {
    let rest = text;
    let removed = 0;
    let change: CommandChange = {};
    // Bounded: each pass removes at least two characters.
    for (let i = 0; i < 50; i++) {
        const result = parseCommand(rest, sessions);
        if (result.kind === "none") return { text: rest, change, removed, error: null };
        if (result.kind === "error") return { text: rest, change, removed, error: result.message };
        change = { ...change, ...result.change };
        rest = result.text;
        removed += result.removed;
    }
    return { text: rest, change, removed, error: null };
}

// ── The strip ────────────────────────────────────────────────────────────────

export type CommandSuggestion =
    | { kind: "command"; command: ComposerCommand }
    | { kind: "session"; session: Session; label: string };

/** How many sessions the strip offers after "/session ". */
export const SESSION_SUGGESTIONS = 8;

/**
 * What the strip offers for the text so far. "/" or "/re" lists the matching commands;
 * "/session " or "/session 1" lists the matching sessions, newest first. Anything
 * else offers nothing, so the strip closes.
 */
export function commandSuggestions(text: string, sessions: readonly Session[]): CommandSuggestion[] {
    const partial = /^\/(\w*)$/.exec(text);
    if (partial) {
        const typed = partial[1]!.toLowerCase();
        return COMPOSER_COMMANDS.filter((c) => c.name.startsWith(typed)).map((command) => ({
            kind: "command",
            command,
        }));
    }
    const session = /^\/session\s+(\d*)$/i.exec(text);
    if (session) {
        const typed = session[1]!;
        return [...sessions]
            .sort((a, b) => b.number - a.number)
            .filter((s) => String(s.number).startsWith(typed))
            .slice(0, SESSION_SUGGESTIONS)
            .map((s) => ({
                kind: "session",
                session: s,
                label: s.isCurrent ? `S${s.number} · current` : `S${s.number}`,
            }));
    }
    return [];
}

/**
 * Picking a suggestion from the strip. /recap, /dm and /me apply at once and clear
 * what was typed; /session asks for its number by leaving "/session " in the text;
 * a session applies and clears the text.
 */
export function pickSuggestion(
    text: string,
    suggestion: CommandSuggestion
): { text: string; change: CommandChange } {
    const typed = /^\/\S*(?:\s+\d*)?/.exec(text)?.[0] ?? "";
    const rest = text.slice(typed.length).replace(/^\s+/, "");
    if (suggestion.kind === "session") return { text: rest, change: sessionChange(suggestion.session) };
    const { name } = suggestion.command;
    if (name === "session") return { text: "/session ", change: {} };
    const change: CommandChange = name === "recap" ? { isRecap: true } : { visibility: name === "dm" ? "DM" : "Me" };
    return { text: rest, change };
}
