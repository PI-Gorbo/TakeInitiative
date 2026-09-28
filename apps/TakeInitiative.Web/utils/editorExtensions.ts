// The composer editor's TipTap extensions and editing commands (step 17, the rich text box), apart from
// the component so the unit tests can run them: `utils/editorText.ts` against the very
// schema the editor uses, and the commands against a real editor.
import { mergeAttributes, type Editor, type Extensions, type Range } from "@tiptap/core";
import Mention from "@tiptap/extension-mention";
import Placeholder from "@tiptap/extension-placeholder";
import { TextSelection } from "@tiptap/pm/state";
import StarterKit from "@tiptap/starter-kit";
import type { SuggestionOptions } from "@tiptap/suggestion";
import { MENTION_NODE, type MentionAttrs } from "./editorText";
import { MENTION_QUERY_MAX, escapeMentionText } from "./mentions";

/**
 * A mention is one atomic chip. It reads `@[Name]` in the text; `raw` keeps its
 * bracket text exactly, and `isNew` marks an entry made by Create (15d).
 */
const EntryMention = Mention.extend({
    name: MENTION_NODE,
    addAttributes() {
        return {
            ...this.parent?.(),
            raw: {
                default: "",
                parseHTML: (element) => element.getAttribute("data-raw") ?? "",
                renderHTML: (attributes) => ({ "data-raw": attributes.raw }),
            },
            isNew: {
                default: false,
                parseHTML: (element) => element.getAttribute("data-new") === "true",
                renderHTML: (attributes) => (attributes.isNew ? { "data-new": "true" } : {}),
            },
        };
    },
});

export function composerExtensions(
    options: {
        placeholder?: () => string;
        /** The `@` suggestion plugin's options (`useEditorMentionPicker`). */
        suggestion?: Omit<SuggestionOptions, "editor">;
        /** A chip's classes: Tailwind scans the components, so they come from there. */
        mentionClass?: (attrs: MentionAttrs) => string;
    } = {}
): Extensions {
    return [
        // Only what the composer's markdown and the note renderer round-trip:
        // paragraphs, bold, italic and bullet lists. Everything else stays markdown
        // source in the text. There are no soft breaks: a line is a paragraph.
        StarterKit.configure({
            blockquote: false,
            code: false,
            codeBlock: false,
            hardBreak: false,
            heading: false,
            horizontalRule: false,
            link: false,
            orderedList: false,
            strike: false,
            trailingNode: false,
            underline: false,
        }),
        Placeholder.configure({ placeholder: options.placeholder ?? "" }),
        EntryMention.configure({
            renderHTML: ({ options: mention, node }) => [
                "span",
                mergeAttributes(mention.HTMLAttributes, { class: options.mentionClass?.(node.attrs as MentionAttrs) ?? "" }),
                `@${node.attrs.label}`,
            ],
            renderText: ({ node }) => `@${node.attrs.label}`,
            ...(options.suggestion ? { suggestion: options.suggestion } : {}),
        }),
    ];
}

// ── Editing ──────────────────────────────────────────────────────────────────
// What the composer's editor does to its document, apart from the component so it is
// tested against a real editor.

/**
 * The `@` query as the picker reads it (15d's rules): `@[Gund` is "Gund"; a query that
 * starts with a space, has a closed bracket, or is past 40 characters is none.
 */
export function mentionQuery(query: string): string | null {
    let text = query;
    if (text.startsWith("[")) {
        if (text.includes("]")) return null;
        text = text.slice(1);
    }
    if (text.length > MENTION_QUERY_MAX || /^\s/.test(text)) return null;
    return text;
}

/** Replaces the `@` query at `range` with a mention, and a space unless one follows. */
export function insertMention(editor: Editor, range: Range, entry: { id: string; name: string; isNew: boolean }) {
    const after = editor.state.doc.resolve(range.to).nodeAfter;
    const attrs: MentionAttrs = { id: entry.id, label: entry.name, raw: escapeMentionText(entry.name), isNew: entry.isNew };
    return editor
        .chain()
        .focus()
        .insertContentAt(range, [
            { type: MENTION_NODE, attrs },
            ...(after?.isText && /^\s/.test(after.text ?? "") ? [] : [{ type: "text", text: " " }]),
        ])
        .run();
}

/**
 * The `@` toolbar button and Mod+K: inserts `@` at the caret, with a space before it
 * unless the caret starts a line or follows a space or `(`. A selection becomes the
 * query, as `insertMentionTrigger` does in a textarea.
 */
export function insertMentionTrigger(editor: Editor) {
    const { from, to, $from } = editor.state.selection;
    const before = $from.nodeBefore;
    const spacer = $from.parentOffset === 0 || (before?.isText && /[\s(]$/.test(before.text ?? "")) ? "" : " ";
    const inserted = `${spacer}@`;
    return editor
        .chain()
        .focus()
        .insertContentAt(from, { type: "text", text: inserted })
        .setTextSelection(to + inserted.length)
        .run();
}

/** Enter as a new line: a new list item (or out of the list from an empty one), else a new paragraph. */
export function newLine(editor: Editor) {
    return editor.commands.first(({ commands }) => [
        () => commands.splitListItem("listItem"),
        () => commands.liftEmptyBlock(),
        () => commands.splitBlock(),
    ]);
}

// The `/` commands (14e) are at the start of the text, so an offset there is a
// position in the first paragraph.

/** The caret as an offset into the text while it is in the first line; else null. */
export function caretOffset(editor: Editor): number | null {
    const { doc, selection } = editor.state;
    const first = doc.firstChild;
    if (first?.type.name !== "paragraph" || selection.from > first.content.size + 1) return null;
    return doc.textBetween(1, selection.from).length;
}

/** Puts the caret at an offset into the first line, or at the end past it. */
export function setCaretOffset(editor: Editor, offset: number) {
    const { doc, tr } = editor.state;
    const first = doc.firstChild;
    const selection =
        first?.type.name === "paragraph" && offset <= first.content.size
            ? TextSelection.create(doc, 1 + offset)
            : TextSelection.atEnd(doc);
    editor.view.dispatch(tr.setSelection(selection));
}
