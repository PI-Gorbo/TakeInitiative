// The session note renderer. `NoteMarkdown.vue` is the only place its output meets
// `v-html`, and nothing else is ever rendered with `v-html`.
//
// Safety: `html: false` escapes raw HTML instead of passing it through (the XSS
// guard), markdown-it's `validateLink` drops `javascript:`, `vbscript:`, `file:` and
// `data:` links, and images are off (a note is text in step 14; images arrive in
// step 16 as uploads, never as remote URLs in the text).
import MarkdownIt, { type StateCore, type Token } from "markdown-it";
import type { EntryKind } from "./api/types";
import { ENTRY_KIND_ICONS } from "./entries";

/**
 * What the renderer needs to draw mention chips, passed as markdown-it's `env`.
 * `resolve` answers from the viewer's entry directory (`utils/entries.ts`): an id
 * that is not there (unknown, or an entry the viewer cannot see) is drawn as plain
 * text, with no link and no hint (glossary: Mention chip).
 */
export type NoteMarkdownEnv = {
    campaignId: string;
    resolve: (entryId: string) => { id: string; kind: EntryKind } | undefined;
    /** An article block (15f): a document, so headings stay headings. */
    document?: boolean;
};

/** A mention as the API's `MentionParser` reads it (15b). */
export type NoteMention = { entryId: string; text: string };

const md = new MarkdownIt({
    html: false,
    linkify: true,
    breaks: true,
    typographer: false,
});
md.disable(["image"]);

// A note is a chat line, not a document: headings render as bold paragraphs. An
// article is a document (15f's `document` option), so there they stay headings.
md.renderer.rules.heading_open = (tokens, idx, options, env: Partial<NoteMarkdownEnv>, self) =>
    env?.document ? self.renderToken(tokens, idx, options) : "<p><strong>";
md.renderer.rules.heading_close = (tokens, idx, options, env: Partial<NoteMarkdownEnv>, self) =>
    env?.document ? self.renderToken(tokens, idx, options) : "</strong></p>\n";

// Links open in a new tab and pass nothing on.
md.renderer.rules.link_open = (tokens, idx, options, _env, self) => {
    const token = tokens[idx];
    token.attrSet("target", "_blank");
    token.attrSet("rel", "noopener noreferrer nofollow");
    return self.renderToken(tokens, idx, options);
};

// ── Mentions (15b's rule, shared with the API through Fixtures/mentions.json) ──
// A mention is stored as `@[text](entry:<id>)`. Markdown reads that as an "@" and a
// link. It is a mention when:
// - the token right before the link is a `text` token ending in "@". An escaped
//   `\@` is a `text_special` token here, because this rule runs before `text_join`;
// - the href is a lower-case `entry:` plus a GUID in its 36-character form;
// - the link is an inline link, not an autolink (`<entry:…>`) or a linkified URL.
// Such a link becomes an `entry_mention_open` / `entry_mention_close` pair and the
// "@" is dropped. Any other `entry:` link becomes an `entry_link_*` pair, drawn as its
// plain text: it is not a mention, so it links nowhere.
const MENTION_HREF = /^entry:([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$/;
const ENTRY_PREFIX = /^entry:/i;

function entryMentions(state: StateCore) {
    for (const block of state.tokens) {
        if (block.type !== "inline" || !block.children) continue;
        const children = block.children;
        for (let i = 0; i < children.length; i++) {
            const token = children[i];
            if (token.type !== "link_open") continue;
            const href = String(token.attrGet("href") ?? "");
            if (!ENTRY_PREFIX.test(href)) continue;
            const closeIndex = findLinkClose(children, i);
            if (closeIndex === undefined) continue;
            const close = children[closeIndex];

            const before = children[i - 1];
            const match = MENTION_HREF.exec(href);
            const isInline = token.markup !== "autolink" && token.markup !== "linkify";
            if (match && isInline && before?.type === "text" && before.content.endsWith("@")) {
                const entryId = match[1].toLowerCase();
                const text = plainText(children.slice(i + 1, closeIndex));
                token.type = "entry_mention_open";
                token.meta = { entryId, text } satisfies NoteMention;
                close.type = "entry_mention_close";
                close.meta = { entryId };
                before.content = before.content.slice(0, -1);
            } else {
                token.type = "entry_link_open";
                close.type = "entry_link_close";
            }
        }
    }
}

function findLinkClose(children: Token[], openIndex: number): number | undefined {
    let depth = 0;
    for (let j = openIndex + 1; j < children.length; j++) {
        if (children[j].type === "link_open") depth++;
        if (children[j].type === "link_close") {
            if (depth === 0) return j;
            depth--;
        }
    }
    return undefined;
}

/** The link text as plain text: emphasis dropped, code kept, breaks as spaces. */
function plainText(tokens: Token[]): string {
    let text = "";
    for (const token of tokens) {
        if (token.type === "text" || token.type === "text_special" || token.type === "code_inline") text += token.content;
        else if (token.type === "softbreak" || token.type === "hardbreak") text += " ";
    }
    return text.trim();
}

md.core.ruler.after("inline", "entry_mentions", entryMentions);

const escape = md.utils.escapeHtml;

/** The mention chip: its text, linking to the entry, with the kind's icon (15c). */
md.renderer.rules.entry_mention_open = (tokens, idx, _options, env: Partial<NoteMarkdownEnv>) => {
    const entry = resolveMention(tokens[idx], env);
    if (!entry || !env.campaignId) return "";
    const href = `/app/campaigns/${encodeURIComponent(env.campaignId)}/wiki/${encodeURIComponent(entry.id)}`;
    return (
        `<a class="mention" data-entry-id="${escape(entry.id)}" data-kind="${escape(entry.kind)}" href="${escape(href)}">` +
        `<span class="mention-icon" aria-hidden="true">${ENTRY_KIND_ICONS[entry.kind] ?? ""}</span>`
    );
};
md.renderer.rules.entry_mention_close = (tokens, idx, _options, env: Partial<NoteMarkdownEnv>) =>
    resolveMention(tokens[idx], env) && env.campaignId ? "</a>" : "";

md.renderer.rules.entry_link_open = () => "";
md.renderer.rules.entry_link_close = () => "";

function resolveMention(token: Token, env: Partial<NoteMarkdownEnv> | undefined) {
    const entryId = (token.meta as { entryId?: string } | null)?.entryId;
    return entryId && env?.resolve ? env.resolve(entryId) : undefined;
}

/**
 * Renders a session note's markdown to safe HTML. With an `env`, mentions of entries
 * the viewer can see are chips; without one, every mention is its plain text.
 */
export function renderNoteMarkdown(text: string, env?: NoteMarkdownEnv): string {
    return md.render(text, env ?? {});
}

/**
 * Every mention in a note's text, in order, repeats included. The same rule as the
 * API's `MentionParser.Parse`; `Fixtures/mentions.json` keeps the two in step.
 */
export function noteMentions(text: string): NoteMention[] {
    const mentions: NoteMention[] = [];
    for (const block of md.parse(text, {})) {
        for (const token of block.children ?? []) {
            if (token.type === "entry_mention_open") mentions.push({ ...(token.meta as NoteMention) });
        }
    }
    return mentions;
}

/** The distinct entry ids a note mentions, in order of first mention. */
export function mentionedEntryIds(text: string): string[] {
    return [...new Set(noteMentions(text).map((m) => m.entryId))];
}
