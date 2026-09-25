// The session note renderer. `NoteMarkdown.vue` is the only place its output meets
// `v-html`, and nothing else is ever rendered with `v-html`.
//
// Safety: `html: false` escapes raw HTML instead of passing it through (the XSS
// guard), markdown-it's `validateLink` drops `javascript:`, `vbscript:`, `file:` and
// `data:` links, and images are off (a note is text in step 14; images arrive in
// step 16 as uploads, never as remote URLs in the text).
import MarkdownIt, { type StateCore, type Token } from "markdown-it";

const ENTRY_PREFIX = "entry:";

const md = new MarkdownIt({ html: false, linkify: true, breaks: true, typographer: false });
md.disable(["image"]);

// A note is a chat line, not a document: headings render as bold paragraphs.
md.renderer.rules.heading_open = () => "<p><strong>";
md.renderer.rules.heading_close = () => "</strong></p>\n";

// Links open in a new tab and pass nothing on.
md.renderer.rules.link_open = (tokens, idx, options, _env, self) => {
    const token = tokens[idx];
    token.attrSet("target", "_blank");
    token.attrSet("rel", "noopener noreferrer nofollow");
    return self.renderToken(tokens, idx, options);
};

// ── The `@` seam (step 15) ────────────────────────────────────────────────────
// A mention is stored as `@[text](entry:<id>)`. Markdown reads that as an "@" and
// a link. This core rule turns such a link into an `entry_mention_open` /
// `entry_mention_close` pair, drops the "@" before it, and the renderer rules
// below draw the link text as plain text. Step 15 replaces `renderEntryMention*`
// with a mention chip; nothing else needs to change.
function entryMentions(state: StateCore) {
    for (const block of state.tokens) {
        if (block.type !== "inline" || !block.children) continue;
        const children = block.children;
        for (let i = 0; i < children.length; i++) {
            const token = children[i];
            if (token.type !== "link_open") continue;
            const href = String(token.attrGet("href") ?? "");
            if (!href.startsWith(ENTRY_PREFIX)) continue;

            token.type = "entry_mention_open";
            token.meta = { entryId: href.slice(ENTRY_PREFIX.length) };
            const close = findLinkClose(children, i);
            if (close) close.type = "entry_mention_close";

            const before = children[i - 1];
            if (before?.type === "text" && before.content.endsWith("@")) {
                before.content = before.content.slice(0, -1);
            }
        }
    }
}

function findLinkClose(children: Token[], openIndex: number): Token | undefined {
    let depth = 0;
    for (let j = openIndex + 1; j < children.length; j++) {
        if (children[j].type === "link_open") depth++;
        if (children[j].type === "link_close") {
            if (depth === 0) return children[j];
            depth--;
        }
    }
    return undefined;
}

md.core.ruler.after("inline", "entry_mentions", entryMentions);

/** Step 15 swaps these two for a mention chip. In 14 a mention is its text. */
md.renderer.rules.entry_mention_open = () => "";
md.renderer.rules.entry_mention_close = () => "";

/** Renders a session note's markdown to safe HTML. */
export function renderNoteMarkdown(text: string): string {
    return md.render(text);
}
