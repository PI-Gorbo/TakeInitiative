// An entry's article in the editor (15f, design §4 and §3a). Pure, so it is unit
// tested without Nuxt; `ArticleEditor.vue` holds the blocks and calls these.
//
// The API sends the viewer's view of the article: the blocks they can see, in order
// (15e). The editor turns each into an `EditorBlock` whose text reads mentions as
// `@[Name]` (15d's `MentionText`, one per block), and saves the whole view back with
// the etag it was loaded with. Blocks the viewer cannot see never reach the web, and
// the server puts them back in place.
import type { ArticleBlock, Quote, Visibility } from "./api/types";
import type { EntryDirectory, EntryViewer } from "./entries";
import {
    displayTokens,
    fromStoredText,
    mentionBody,
    pruneMentionText,
    relinkNewEntry,
    revealCheck,
    type MentionText,
    type NewEntry,
    type RevealItem,
} from "./mentions";

// ── Blocks ───────────────────────────────────────────────────────────────────

/** One block in the editor. `id` is null for a block the viewer added. */
export type EditorBlock = {
    /** Local and unique: what the block's text box is keyed by. A new key remounts it. */
    key: string;
    id: string | null;
    visibility: Visibility;
    /** A secret block's `DM` and `Me` are relative to its owner (15e). */
    ownerMemberId: string;
    quote: Quote | null;
    mention: MentionText;
};

/** Ordinary text, a secret block (🔒 DM / 🔒 Me) or a quote (glossary: Block). */
export type BlockKind = "text" | "secret" | "quote";

export function blockKind(block: Pick<EditorBlock, "visibility"> & { quote?: Quote | null }): BlockKind {
    if (block.quote) return "quote";
    return block.visibility === "Everyone" ? "text" : "secret";
}

/** A block's 🔒 label, or null for an `Everyone` block. A quote of a 🔒 note has one too. */
export const secretLabel = (visibility: Visibility): string | null =>
    visibility === "Everyone" ? null : `🔒 ${visibility}`;

/**
 * Who reads a secret block, for its tooltip: "Visible to the DMs and Sam". `DM` and
 * `Me` are relative to the owner.
 */
export function secretAudience(visibility: Visibility, ownerName: string, ownerIsViewer: boolean): string {
    const owner = ownerIsViewer ? "you" : ownerName;
    if (visibility === "DM") return `Visible to the DMs and ${owner}`;
    if (visibility === "Me") return ownerIsViewer ? "Visible only to you" : `Visible only to ${ownerName}`;
    return "Visible to everyone who can see this entry";
}

/**
 * Whether the viewer may change a block's visibility: its owner and the DMs (15e; the
 * API answers anyone else with a 403). A new block is the viewer's own.
 */
export const canChangeBlockVisibility = (block: Pick<EditorBlock, "ownerMemberId">, viewer: EntryViewer) =>
    viewer.isDm || block.ownerMemberId === viewer.memberId;

/** The loaded article's blocks, ready to edit. */
export function toEditorBlocks(blocks: readonly ArticleBlock[], newKey: () => string): EditorBlock[] {
    return blocks.map((block) => ({
        key: newKey(),
        id: block.id,
        visibility: block.visibility,
        ownerMemberId: block.ownerMemberId,
        quote: block.quote ?? null,
        mention: { ...fromStoredText(block.text), newEntries: [] },
    }));
}

/** "+ Text" and "+ 🔒 Secret": an empty block the viewer owns. */
export function newBlock(kind: "text" | "secret", viewer: EntryViewer, newKey: () => string): EditorBlock {
    return {
        key: newKey(),
        id: null,
        visibility: kind === "secret" ? "DM" : "Everyone",
        ownerMemberId: viewer.memberId,
        quote: null,
        mention: { text: "", links: {}, newEntries: [] },
    };
}

/** Moves a block up (-1) or down (+1). Out of range leaves the list as it is. */
export function moveBlock(blocks: readonly EditorBlock[], index: number, delta: -1 | 1): EditorBlock[] {
    const to = index + delta;
    if (index < 0 || index >= blocks.length || to < 0 || to >= blocks.length) return [...blocks];
    const out = [...blocks];
    [out[index], out[to]] = [out[to]!, out[index]!];
    return out;
}

export const removeBlock = (blocks: readonly EditorBlock[], index: number): EditorBlock[] =>
    blocks.filter((_, i) => i !== index);

/**
 * The `SecretBlockPicker`: 🔒 DM, 🔒 Me, or back to `Everyone`. Only for the block's
 * owner and the DMs; anyone else gets the list unchanged.
 */
export function setBlockVisibility(
    blocks: readonly EditorBlock[],
    index: number,
    visibility: Visibility,
    viewer: EntryViewer
): EditorBlock[] {
    const block = blocks[index];
    if (!block || !canChangeBlockVisibility(block, viewer)) return [...blocks];
    return blocks.map((b, i) => (i === index ? { ...b, visibility } : b));
}

// ── 🔒: wrapping a selection or the current block ────────────────────────────

export type TextRange = { start: number; end: number };

/** A range widened so it never cuts through an `@[…]` mention. */
export function widenToMentions(text: string, range: TextRange): TextRange {
    let { start, end } = range;
    for (const token of displayTokens(text)) {
        if (start > token.start && start < token.end) start = token.start;
        if (end > token.start && end < token.end) end = token.end;
    }
    return { start, end };
}

/** Part of a block's text, keeping only the links and new entries that part uses. */
function slice(state: MentionText, text: string): MentionText {
    return pruneMentionText({
        text,
        links: { ...state.links },
        newEntries: [...state.newEntries],
    });
}

/**
 * The 🔒 button (design §3a) on an ordinary block:
 * - with a selection, the block splits into three and the middle becomes a secret
 *   block the viewer owns. The first non-blank ordinary part keeps the block's id;
 *   the others are new. A selection never cuts a mention in two;
 * - without one, the whole block becomes secret. When the viewer may not change its
 *   visibility (someone else's block), it becomes a new secret block the viewer owns,
 *   which is the same as selecting all of it.
 * Every resulting block gets a new key. Returns null for a quote or a secret block,
 * whose visibility is the `SecretBlockPicker`'s.
 */
export function wrapSecret(
    blocks: readonly EditorBlock[],
    index: number,
    selection: TextRange | null,
    viewer: EntryViewer,
    newKey: () => string,
    visibility: Exclude<Visibility, "Everyone"> = "DM"
): { blocks: EditorBlock[]; focusIndex: number } | null {
    const block = blocks[index];
    if (!block || blockKind(block) !== "text") return null;
    const text = block.mention.text;
    const range =
        selection && selection.end > selection.start
            ? widenToMentions(text, {
                  start: Math.max(0, Math.min(selection.start, text.length)),
                  end: Math.max(0, Math.min(selection.end, text.length)),
              })
            : null;
    const replace = (parts: EditorBlock[], focus: number) => ({
        blocks: [...blocks.slice(0, index), ...parts, ...blocks.slice(index + 1)],
        focusIndex: index + focus,
    });

    const middle = range ? text.slice(range.start, range.end).trim() : "";
    const before = range ? text.slice(0, range.start).trimEnd() : "";
    const after = range ? text.slice(range.end).trimStart() : "";
    if (!range || middle === "" || (before === "" && after === "")) {
        // The whole block.
        const own = canChangeBlockVisibility(block, viewer);
        return replace(
            [
                {
                    ...block,
                    key: newKey(),
                    visibility,
                    ...(own ? {} : { id: null, ownerMemberId: viewer.memberId }),
                    mention: slice(block.mention, text),
                },
            ],
            0
        );
    }

    const parts: EditorBlock[] = [];
    let idTaken = false;
    const ordinary = (partText: string): EditorBlock => {
        const keepsId = !idTaken;
        idTaken = true;
        return {
            ...block,
            key: newKey(),
            ...(keepsId ? {} : { id: null, ownerMemberId: viewer.memberId }),
            mention: slice(block.mention, partText),
        };
    };
    if (before !== "") parts.push(ordinary(before));
    const focus = parts.length;
    parts.push({
        key: newKey(),
        id: null,
        visibility,
        ownerMemberId: viewer.memberId,
        quote: null,
        mention: slice(block.mention, middle),
    });
    if (after !== "") parts.push(ordinary(after));
    return replace(parts, focus);
}

// ── Saving ───────────────────────────────────────────────────────────────────

export type ArticleSaveBlock = {
    id?: string;
    text: string;
    visibility: Visibility;
};
export type ArticleSaveBody = {
    etag: string;
    blocks: ArticleSaveBlock[];
    newEntries: NewEntry[];
};

/** A block as the API stores it: the stored text (mentions as `@[t](entry:<id>)`), trimmed. */
export const storedBlockText = (block: Pick<EditorBlock, "mention">) => mentionBody(block.mention).text.trim();

/**
 * `PUT …/article`'s body (15e): every block the editor shows, in order, with `id` for
 * an existing block and none for a new one. Blank blocks are left out (the server
 * removes them), adjacent ordinary blocks are joined into one, which keeps the first
 * id in the run, and each block's pending new entries are merged.
 */
export function articleSaveBody(etag: string, blocks: readonly EditorBlock[]): ArticleSaveBody {
    const out: ArticleSaveBlock[] = [];
    const newEntries = new Map<string, NewEntry>();
    let joinable = false;
    for (const block of blocks) {
        const body = mentionBody(block.mention);
        const text = body.text.trim();
        if (text === "") continue;
        for (const entry of body.newEntries) newEntries.set(entry.id.toLowerCase(), entry);
        const ordinary = blockKind(block) === "text";
        const last = out[out.length - 1];
        if (ordinary && joinable && last) {
            last.text = `${last.text}\n\n${text}`;
            if (last.id === undefined && block.id) last.id = block.id;
            continue;
        }
        out.push({
            ...(block.id ? { id: block.id } : {}),
            text,
            visibility: block.visibility,
        });
        joinable = ordinary;
    }
    return { etag, blocks: out, newEntries: [...newEntries.values()] };
}

/** True when saving would change nothing: the same blocks, text and visibility, in order. */
export function articleUnchanged(original: readonly ArticleBlock[], body: ArticleSaveBody): boolean {
    return (
        body.newEntries.length === 0 &&
        body.blocks.length === original.length &&
        body.blocks.every(
            (b, i) =>
                b.id === original[i]!.id &&
                b.text === original[i]!.text.trim() &&
                b.visibility === original[i]!.visibility
        )
    );
}

/**
 * The blocks the viewer changed, for the conflict dialog (design §4: reload and
 * re-apply): new blocks, and blocks whose text or visibility differs from the loaded
 * article. `text` is the stored form, so pasting it back keeps the mentions linked.
 */
export type EditedBlock = {
    key: string;
    kind: BlockKind;
    visibility: Visibility;
    display: string;
    text: string;
};

export function editedBlocks(original: readonly ArticleBlock[], blocks: readonly EditorBlock[]): EditedBlock[] {
    const byId = new Map(original.map((b) => [b.id.toLowerCase(), b]));
    const out: EditedBlock[] = [];
    for (const block of blocks) {
        const text = storedBlockText(block);
        if (text === "") continue;
        const before = block.id ? byId.get(block.id.toLowerCase()) : undefined;
        if (before && before.text.trim() === text && before.visibility === block.visibility) continue;
        out.push({
            key: block.key,
            kind: blockKind(block),
            visibility: block.visibility,
            display: block.mention.text.trim(),
            text,
        });
    }
    return out;
}

/** After a duplicate-name 409 (15b's `relinkNewEntry`), in every block. */
export function relinkArticleNewEntry(
    blocks: readonly EditorBlock[],
    newEntryId: string,
    existingEntryId: string
): EditorBlock[] {
    return blocks.map((b) => ({
        ...b,
        mention: relinkNewEntry(b.mention, newEntryId, existingEntryId),
    }));
}

// ── The reveal warning (15d) ─────────────────────────────────────────────────

const REACH: Record<Visibility, number> = { Everyone: 0, DM: 1, Me: 2 };
const narrower = (a: Visibility, b: Visibility): Visibility => (REACH[a] >= REACH[b] ? a : b);

/**
 * 15d's reveal check over an article. A block is read by the entry's audience narrowed
 * by its own visibility. Returns the entries hidden from some block's readers, each
 * once, and the widest audience that warned (what the dialog names).
 */
export function articleRevealCheck(
    entryVisibility: Visibility,
    blocks: readonly Pick<ArticleSaveBlock, "text" | "visibility">[],
    directory: EntryDirectory,
    viewer: EntryViewer
): { audience: Visibility; items: RevealItem[] } {
    const items = new Map<string, RevealItem>();
    let audience: Visibility | null = null;
    for (const block of blocks) {
        const readers = narrower(entryVisibility, block.visibility);
        const warn = revealCheck(readers, block.text, directory, viewer);
        if (warn.length === 0) continue;
        audience = audience === null || REACH[readers] < REACH[audience] ? readers : audience;
        for (const item of warn) items.set(item.entry.id.toLowerCase(), item);
    }
    return {
        audience: audience ?? entryVisibility,
        items: [...items.values()],
    };
}

/** The entry a quote was promoted into already quotes this note (the web warns, 15e). */
export const quotesNote = (blocks: readonly ArticleBlock[] | undefined, noteId: string) =>
    !!blocks?.some((b) => b.quote?.noteId.toLowerCase() === noteId.toLowerCase());

// ── Links ────────────────────────────────────────────────────────────────────

/** `/wiki/{entryId}?edit={blockId}`: the entry page opens its article editor at a block. */
export const EDIT_BLOCK_PARAM = "edit";

export const entryHref = (campaignId: string, entryId: string, editBlockId?: string) =>
    `/app/campaigns/${encodeURIComponent(campaignId)}/wiki/${encodeURIComponent(entryId)}` +
    (editBlockId ? `?${EDIT_BLOCK_PARAM}=${encodeURIComponent(editBlockId)}` : "");
