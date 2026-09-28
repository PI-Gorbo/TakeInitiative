// @vitest-environment happy-dom
import { Editor, type Range } from "@tiptap/core";
import { PluginKey } from "@tiptap/pm/state";
import { exitSuggestion } from "@tiptap/suggestion";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
    caretOffset,
    composerExtensions,
    insertMention,
    insertMentionTrigger,
    mentionQuery,
    newLine,
    setCaretOffset,
} from "~/utils/editorExtensions";
import { docToText, textToDoc } from "~/utils/editorText";

const GUNDREN = "11111111-1111-4111-8111-111111111111";

// A real editor with the composer's extensions, and the `@` suggestion's state as the
// picker sees it (`useEditorMentionPicker`).
let editor: Editor | undefined;
function setup(options: { text?: string; handleKeyDown?: (event: KeyboardEvent) => boolean } = {}) {
    const pluginKey = new PluginKey("testMention");
    const query: { current: { range: Range; query: string } | null } = { current: null };
    const onUpdate = vi.fn();
    editor = new Editor({
        element: document.createElement("div"),
        content: textToDoc({ text: options.text ?? "", links: {} }),
        extensions: composerExtensions({
            suggestion: {
                char: "@",
                pluginKey,
                allowSpaces: true,
                allowedPrefixes: [" ", "(", " ", "\t"],
                items: () => [],
                render: () => ({
                    onStart: (props) => (query.current = { range: props.range, query: props.query }),
                    onUpdate: (props) => (query.current = { range: props.range, query: props.query }),
                    onExit: () => (query.current = null),
                }),
            },
        }),
        editorProps: options.handleKeyDown ? { handleKeyDown: (_view, event) => options.handleKeyDown!(event) } : {},
        onUpdate,
    });
    return { editor, query, pluginKey, onUpdate, text: () => docToText(editor!.getJSON()) };
}

afterEach(() => {
    editor?.destroy();
    editor = undefined;
});

/** Types at the caret, as the keyboard would (the suggestion plugin watches the selection). */
const type = (target: Editor, text: string) => target.commands.insertContent(text);

describe("the @ suggestion in the editor", () => {
    it("opens after a space, a ( or at a line's start, with spaces in the query", () => {
        const { editor, query } = setup();
        type(editor, "@Gun");
        expect(query.current?.query).toBe("Gun");
        type(editor, "dren Rock");
        expect(query.current?.query).toBe("Gundren Rock");

        editor.commands.setContent(textToDoc({ text: "", links: {} }));
        type(editor, "ask (@Sil");
        expect(query.current?.query).toBe("Sil");
    });

    it("does not open inside a word, as in an email address", () => {
        const { editor, query } = setup();
        type(editor, "me@example");
        expect(query.current).toBeNull();
    });

    it("stays closed after Esc until another @ starts", () => {
        const { editor, query, pluginKey } = setup();
        type(editor, "@Gun");
        exitSuggestion(editor.view, pluginKey);
        expect(query.current).toBeNull();
        type(editor, "d");
        expect(query.current).toBeNull();
        type(editor, " @Sil");
        expect(query.current?.query).toBe("Sil");
    });

    it("reads the query with the textarea picker's rules", () => {
        expect(mentionQuery("[Gund")).toBe("Gund");
        expect(mentionQuery("[Gund]")).toBeNull();
        expect(mentionQuery(" Gund")).toBeNull();
        expect(mentionQuery("x".repeat(41))).toBeNull();
    });
});

describe("picking and the @ button", () => {
    it("replaces the query with a mention and a space", () => {
        const { editor, query, text } = setup();
        type(editor, "met @Gun");
        insertMention(editor, query.current!.range, { id: GUNDREN, name: "Gundren", isNew: false });
        expect(text()).toEqual({ text: "met @[Gundren] ", links: { Gundren: GUNDREN } });
        expect(query.current).toBeNull();
        type(editor, "today");
        expect(text().text).toBe("met @[Gundren] today");
    });

    it("adds no second space when one follows", () => {
        const { editor, query, text } = setup({ text: "@Gun and more" });
        editor.commands.setTextSelection(5);
        insertMention(editor, query.current!.range, { id: GUNDREN, name: "Gundren", isNew: false });
        expect(text().text).toBe("@[Gundren] and more");
    });

    it("escapes a name's markdown in the mention's text", () => {
        const { editor, query, text } = setup();
        type(editor, "@R");
        insertMention(editor, query.current!.range, { id: GUNDREN, name: "R_2*", isNew: true });
        expect(text()).toEqual({ text: "@[R\\_2\\*] ", links: { "R\\_2\\*": GUNDREN } });
    });

    it("inserts @ with a space before it unless one is there, and opens the query", () => {
        const { editor, query, text } = setup({ text: "hi" });
        editor.commands.focus("end");
        insertMentionTrigger(editor);
        expect(text().text).toBe("hi @");
        expect(query.current?.query).toBe("");

        editor.commands.setContent(textToDoc({ text: "", links: {} }));
        insertMentionTrigger(editor);
        expect(text().text).toBe("@");
    });

    it("makes a selection the query", () => {
        const { editor, query } = setup({ text: "ask Gund" });
        editor.commands.setTextSelection({ from: 5, to: 9 });
        insertMentionTrigger(editor);
        expect(query.current?.query).toBe("Gund");
    });
});

describe("keys and formatting", () => {
    it("runs the editor's own key handler ahead of the suggestion plugin", () => {
        const seen: string[] = [];
        const { editor, query } = setup({
            handleKeyDown: (event) => {
                seen.push(event.key);
                return event.key === "Enter";
            },
        });
        type(editor, "@Gun");
        editor.view.dom.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
        expect(seen).toEqual(["Enter"]);
        // Handled first, so the suggestion plugin did not see it and the query is still open.
        expect(query.current?.query).toBe("Gun");
    });

    it("writes bold, italic and lists from their commands", () => {
        const { editor, text } = setup();
        editor.chain().toggleBold().insertContent("10gp").toggleBold().insertContent(" each ").run();
        editor.chain().toggleItalic().insertContent("quietly").run();
        expect(text().text).toBe("**10gp** each *quietly*");
        editor.commands.toggleBulletList();
        expect(text().text).toBe("- **10gp** each *quietly*");
    });

    it("makes a new list item on a new line, and leaves the list from an empty one", () => {
        const { editor, text } = setup({ text: "- sword" });
        editor.commands.focus("end");
        newLine(editor);
        type(editor, "shield");
        expect(text().text).toBe("- sword\n- shield");
        newLine(editor);
        newLine(editor);
        type(editor, "done");
        expect(text().text).toBe("- sword\n- shield\ndone");
    });

    it("splits a paragraph on a new line", () => {
        const { editor, text } = setup({ text: "one" });
        editor.commands.focus("end");
        newLine(editor);
        type(editor, "two");
        expect(text().text).toBe("one\ntwo");
    });
});

describe("the text from outside", () => {
    it("replaces the document without an update", () => {
        const { editor, onUpdate, text } = setup({ text: "draft" });
        editor.commands.setContent(textToDoc({ text: "@[Gundren] said", links: { Gundren: GUNDREN } }), {
            emitUpdate: false,
        });
        expect(onUpdate).not.toHaveBeenCalled();
        expect(text()).toEqual({ text: "@[Gundren] said", links: { Gundren: GUNDREN } });
    });

    it("maps the caret to and from an offset in the first line (the / commands)", () => {
        const { editor } = setup({ text: "/recap hello\nsecond" });
        setCaretOffset(editor, 7);
        expect(caretOffset(editor)).toBe(7);
        editor.commands.focus("end");
        expect(caretOffset(editor)).toBeNull();
        setCaretOffset(editor, 100);
        expect(editor.state.selection.from).toBe(editor.state.doc.content.size - 1);
    });
});
