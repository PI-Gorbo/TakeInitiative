import { describe, expect, it } from "vitest";
import type { Session } from "~/utils/api/types";
import {
    COMPOSER_COMMANDS,
    applyCommands,
    commandSuggestions,
    parseCommand,
    pickSuggestion,
} from "~/utils/composerCommands";

const session = (number: number, extra: Partial<Session> = {}): Session => ({
    id: `s${number}`,
    number,
    title: null,
    startedAt: `2026-09-${String(number).padStart(2, "0")}T18:00:00Z`,
    startedByMemberId: "m",
    isCurrent: false,
    ...extra,
});
const sessions = [session(12, { isCurrent: true }), session(11, { title: "Triboar" }), session(2), session(1)];

describe("parseCommand", () => {
    it("consumes /recap, /dm and /me once a space follows, and nothing before", () => {
        expect(parseCommand("/recap", sessions)).toEqual({ kind: "none" });
        expect(parseCommand("/recap We left", sessions)).toEqual({
            kind: "applied",
            command: "recap",
            change: { isRecap: true },
            text: "We left",
            removed: 7,
        });
        expect(parseCommand("/dm ", sessions)).toMatchObject({ change: { visibility: "DM" }, text: "" });
        expect(parseCommand("/me\nsecret", sessions)).toMatchObject({ change: { visibility: "Me" }, text: "secret" });
        expect(parseCommand("/DM x", sessions)).toMatchObject({ change: { visibility: "DM" } });
    });

    it("only counts at the very start of the text", () => {
        expect(parseCommand("hi /dm there", sessions)).toEqual({ kind: "none" });
        expect(parseCommand(" /dm there", sessions)).toEqual({ kind: "none" });
        expect(parseCommand("/dmx there", sessions)).toEqual({ kind: "none" });
        expect(parseCommand("/roll 2d6", sessions)).toEqual({ kind: "none" });
    });

    it("targets a session by number; the current one is stored as nothing picked", () => {
        expect(parseCommand("/session 11 Back then", sessions)).toMatchObject({
            kind: "applied",
            change: { sessionId: "s11" },
            text: "Back then",
            removed: 12,
        });
        expect(parseCommand("/session 12 ", sessions)).toMatchObject({ change: { sessionId: null } });
        expect(parseCommand("/session 11", sessions)).toEqual({ kind: "none" });
        expect(parseCommand("/session ", sessions)).toEqual({ kind: "none" });
    });

    it("an unknown or non-numeric session is an error and leaves the text alone", () => {
        expect(parseCommand("/session 9 text", sessions)).toEqual({ kind: "error", message: "There is no Session 9." });
        expect(parseCommand("/session x text", sessions)).toMatchObject({ kind: "error" });
        expect(applyCommands("/session 9 text", sessions)).toEqual({
            text: "/session 9 text",
            change: {},
            removed: 0,
            error: "There is no Session 9.",
        });
    });
});

describe("applyCommands", () => {
    it("applies a run of pasted commands in order", () => {
        expect(applyCommands("/dm /recap /session 2 We met", sessions)).toEqual({
            text: "We met",
            change: { visibility: "DM", isRecap: true, sessionId: "s2" },
            removed: 22,
            error: null,
        });
    });

    it("the later of two visibilities wins, and a bad session stops the run", () => {
        expect(applyCommands("/dm /me x", sessions).change).toEqual({ visibility: "Me" });
        expect(applyCommands("/recap /session 99 x", sessions)).toMatchObject({
            text: "/session 99 x",
            change: { isRecap: true },
            error: "There is no Session 99.",
        });
    });

    it("leaves ordinary text alone", () => {
        expect(applyCommands("We left Neverwinter", sessions)).toEqual({
            text: "We left Neverwinter",
            change: {},
            removed: 0,
            error: null,
        });
    });
});

describe("commandSuggestions", () => {
    it("lists every command after a lone slash, and narrows as the name is typed", () => {
        expect(commandSuggestions("/", sessions)).toHaveLength(COMPOSER_COMMANDS.length);
        expect(commandSuggestions("/re", sessions).map((s) => s.kind === "command" && s.command.name)).toEqual([
            "recap",
        ]);
        expect(commandSuggestions("/x", sessions)).toEqual([]);
    });

    it("lists sessions, newest first, after /session", () => {
        const labels = (text: string) =>
            commandSuggestions(text, sessions).map((s) => (s.kind === "session" ? s.label : ""));
        expect(labels("/session ")).toEqual(["S12 · current", "S11", "S2", "S1"]);
        expect(labels("/session 1")).toEqual(["S12 · current", "S11", "S1"]);
        expect(labels("/session 11")).toEqual(["S11"]);
    });

    it("offers nothing once a command is complete or not at the start", () => {
        expect(commandSuggestions("/dm ", sessions)).toEqual([]);
        expect(commandSuggestions("text /", sessions)).toEqual([]);
        expect(commandSuggestions("", sessions)).toEqual([]);
    });
});

describe("pickSuggestion", () => {
    const command = (name: string) => commandSuggestions("/", sessions).find((s) => s.kind === "command" && s.command.name === name)!;

    it("applies /recap, /dm and /me and clears what was typed", () => {
        expect(pickSuggestion("/re", command("recap"))).toEqual({ text: "", change: { isRecap: true } });
        expect(pickSuggestion("/", command("me"))).toEqual({ text: "", change: { visibility: "Me" } });
    });

    it("asks for the number after /session, then applies the session", () => {
        expect(pickSuggestion("/s", command("session"))).toEqual({ text: "/session ", change: {} });
        const s11 = commandSuggestions("/session 11", sessions)[0]!;
        expect(pickSuggestion("/session 11", s11)).toEqual({ text: "", change: { sessionId: "s11" } });
    });
});
