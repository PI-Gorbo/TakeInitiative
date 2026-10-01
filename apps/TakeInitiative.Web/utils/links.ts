// An entry's links (27d, 27e): the pure rules behind the Links section — who may read and
// write them, what a row says, where a row goes, and the D&D Beyond sheet preset. The
// components (`Wiki/EntryLinks.vue`, `EntryLinkRow.vue`, `AddLinkSheet.vue`,
// `KnowledgeBasePicker.vue`, `DndBeyondField.vue`) only draw these.
//
// Two kinds, one shape (`EntryLinkKind`): a **knowledge base** link is a foreign key into
// step 26's corpus, resolved on every read, so its name, detail and url come back from the
// corpus and can go stale; an **external** link is a url a member typed with their own label.
// A label is **never** rendered as markdown, and every link opens in a new tab with
// `rel="noopener noreferrer"`.
import type { EntryLink, EntrySummary, KnowledgeBaseItem } from "./api/types";
import { claimerOf, canEditEntry, type EntryViewer } from "./entries";
import { rowLine } from "./knowledgeBase";

// ── The API's limits (EntryLinks) ────────────────────────────────────────────

/** `EntryLinks.MaxPerEntry`: a 21st link is a 409. */
export const LINKS_MAX = 20;
/** `EntryLinks.UrlMaxLength`. */
export const LINK_URL_MAX = 2048;
/** `EntryLinks.LabelMaxLength`, after trimming. */
export const LINK_LABEL_MAX = 80;

// ── Permissions (the API's EntryLinks.CanRead / .CanWrite) ───────────────────

/**
 * Who may **read** an entry's links: `EntryLinks.CanRead`, which is `EntrySources.CanRead`
 * and deliberately not "anyone who can see the entry". An unclaimed Character is an NPC, and
 * "The Eye ↗ Beholder" gives the monster away exactly as its stat block would (invariant 8),
 * so only the DMs read an unclaimed Character's links. Every other kind, and a claimed
 * Character, is read by everyone who can see the entry — which is the D&D Beyond sheet case.
 *
 * The API already redacts: `links` comes back empty for a reader who may not have them, so
 * this is only here to decide whether to draw the section and its `[+]` at all.
 */
export const canReadLinks = (entry: Pick<EntrySummary, "kind" | "claimedByMemberId">, viewer: EntryViewer) =>
    entry.kind !== "Character" || !!claimerOf(entry) || viewer.isDm;

/**
 * Who may **write** them: `EntryLinks.CanWrite` — whoever may edit the entry, **plus the
 * member who plays a claimed Character**.
 *
 * The second clause is 27d's correction to the plan. `canEditEntry` has no claimer clause, so
 * a DM who creates an NPC, hands it to a player and then sets edit access to "Only me" would
 * otherwise lock that player out of their own character's sheet link, which is the whole of
 * 27e. It is only about links: the article, the name and the kind keep `canEditEntry`.
 */
export const canWriteLinks = (
    entry: Pick<EntrySummary, "kind" | "claimedByMemberId" | "creatorMemberId" | "editAccess">,
    viewer: EntryViewer
) => canEditEntry(entry, viewer) || (entry.kind === "Character" && claimerOf(entry) === viewer.memberId);

/**
 * Whether the viewer is offered `[+]`: they can write links **and** read them. A player can
 * edit an unclaimed Character whose edit access is `Anyone`, and the API would take their
 * link — but `canReadLinks` would then hide it from them, so adding one would look like it
 * failed. Only the DMs maintain an NPC's links, which is the same answer the read rule gives.
 */
export const canAddLinks = (
    entry: Pick<EntrySummary, "kind" | "claimedByMemberId" | "creatorMemberId" | "editAccess">,
    viewer: EntryViewer
) => canReadLinks(entry, viewer) && canWriteLinks(entry, viewer);

/**
 * Whether the Links section sits **high** on a phone, under the stats line, rather than inside
 * "More about X" ▸ Details: a claimed Character, because checking your own character's sheet is
 * access pattern 2 in step 25. A knowledge-base link on a Place is not something anyone needs
 * at the table.
 */
export const linksShowHigh = (entry: Pick<EntrySummary, "kind" | "claimedByMemberId">) =>
    entry.kind === "Character" && !!claimerOf(entry);

// ── A url this app will store or render (the API's EntryLinkUrl) ─────────────

/**
 * `EntryLinkUrl.Parse`: absolute, `http` or `https`, at most `LINK_URL_MAX` characters. Null
 * for anything else, which is what refuses `javascript:`, `data:` and `file:` — by naming the
 * two schemes that are allowed rather than the ones that are not.
 *
 * The server rejects these too, and it is the one that matters. This copy is here so the add
 * sheet can say so before the request, and so no view ever puts a url it would have refused
 * into an `href`.
 */
export function parseLinkUrl(url: string | null | undefined): URL | null {
    const text = (url ?? "").trim();
    if (!text || text.length > LINK_URL_MAX) return null;
    let parsed: URL;
    try {
        parsed = new URL(text);
    } catch {
        return null;
    }
    return parsed.protocol === "http:" || parsed.protocol === "https:" ? parsed : null;
}

/** True when a url is one this app will store and render. */
export const isAllowedLinkUrl = (url: string | null | undefined) => parseLinkUrl(url) !== null;

/** The add sheet's message for a url field, in the API's words, or null when it is fine. */
export function linkUrlError(url: string): string | null {
    if (!url.trim()) return "Paste a link.";
    return isAllowedLinkUrl(url)
        ? null
        : `A link must be a http:// or https:// address of at most ${LINK_URL_MAX.toLocaleString("en-US")} characters.`;
}

/** The same for a label: required, trimmed, at most `LINK_LABEL_MAX` characters. */
export function linkLabelError(label: string): string | null {
    const trimmed = label.trim();
    if (!trimmed) return "Give the link a label.";
    return trimmed.length > LINK_LABEL_MAX ? `A label can be at most ${LINK_LABEL_MAX} characters long.` : null;
}

/** `https://docs.google.com/a/b` → `docs.google.com`. The host as a reader recognises it. */
export function displayHost(url: string | null | undefined): string {
    const parsed = parseLinkUrl(url);
    if (!parsed) return "";
    return parsed.hostname.replace(/^www\./i, "");
}

// ── A row ────────────────────────────────────────────────────────────────────

/** A knowledge-base link whose row has gone from the corpus, or that a prune marked. */
export const isStaleLink = (link: Pick<EntryLink, "stale">) => link.stale;

/** The muted line a stale knowledge-base link shows **instead of** its detail. */
export const STALE_LINK_DETAIL = "no longer in your knowledge base";

/**
 * The row's first line. A knowledge-base link is its row's current name, falling back to the
 * stored item id so a staled row is still recognisable; an external link is the member's own
 * label, which is plain text and never markdown.
 */
export function linkTitle(link: Pick<EntryLink, "kind" | "label" | "url" | "name" | "itemId">): string {
    if (link.kind === "External") return link.label?.trim() || displayHost(link.url) || "A link";
    return link.name?.trim() || link.itemId || "A reference item";
}

/**
 * The row's muted second line: "no longer in your knowledge base" for a stale link, the
 * corpus's own detail line for a live one ("Monster · CR 13 · MM"), and the host for an
 * external link ("docs.google.com").
 */
export function linkDetail(
    link: Pick<EntryLink, "kind" | "url" | "detail" | "providerLabel" | "stale">
): string {
    if (link.kind === "External") return displayHost(link.url);
    if (isStaleLink(link)) return STALE_LINK_DETAIL;
    return link.detail?.trim() || link.providerLabel || "";
}

/**
 * How the row opens, or **null** when there is nowhere honest to send the reader: a stale
 * knowledge-base link's `url` comes back null, so it renders muted with no `↗` and is not a
 * link at all. It is still removable — the row going from the corpus has nothing to do with
 * the member's right to their own link.
 *
 * `target="_blank"` with `rel="noopener noreferrer"` on every link, whatever its kind:
 * `noopener` keeps the new tab from reaching back into this one, and `noreferrer` keeps our
 * url, which carries a campaign id, out of the destination's logs.
 */
export function linkAnchor(link: Pick<EntryLink, "url">) {
    const parsed = parseLinkUrl(link.url);
    return parsed ? ({ href: link.url as string, target: "_blank", rel: "noopener noreferrer" } as const) : null;
}

/** "Opens docs.google.com in a new tab", read out after the title. */
export const linkHint = (link: Pick<EntryLink, "kind" | "url" | "providerLabel">) =>
    `Opens ${(link.kind === "KnowledgeBase" ? link.providerLabel : null) || displayHost(link.url) || "a new page"} in a new tab`;

// ── Optimistic links (14d's pattern) ─────────────────────────────────────────

const PENDING_PREFIX = "pending-";

/** Whether a row is the optimistic copy of an add still on its way: it cannot be removed yet. */
export const isPendingLink = (link: Pick<EntryLink, "id">) => link.id.startsWith(PENDING_PREFIX);

const newPendingLinkId = () => `${PENDING_PREFIX}${Math.random().toString(36).slice(2)}${Date.now().toString(36)}`;

/** The row shown at once for an external link, until `POST links` answers with the real one. */
export const pendingExternalLink = (url: string, label: string, memberId: string): EntryLink => ({
    id: newPendingLinkId(),
    kind: "External",
    addedAt: new Date().toISOString(),
    addedByMemberId: memberId,
    url: url.trim(),
    label: label.trim(),
    hasStatBlock: false,
    stale: false,
});

/**
 * The same for a knowledge-base link, drawn from the picked row. The real link stores only
 * `(provider, itemId)` and the server resolves the rest, so these fields are this row as the
 * picker had it — exactly what the response will say, and replaced by it either way.
 */
export const pendingKnowledgeBaseLink = (item: KnowledgeBaseItem, memberId: string): EntryLink => ({
    id: newPendingLinkId(),
    kind: "KnowledgeBase",
    addedAt: new Date().toISOString(),
    addedByMemberId: memberId,
    provider: item.provider,
    providerLabel: item.providerLabel ?? item.provider,
    itemId: item.id,
    name: item.name,
    detail: rowLine(item),
    bookTitle: item.bookTitle,
    imageUrl: item.imageUrl,
    url: item.url,
    hasStatBlock: false,
    stale: false,
});

/** Whether the entry already links a knowledge-base row: the picker marks it rather than offering a 409. */
export const linksItem = (links: readonly EntryLink[], item: Pick<KnowledgeBaseItem, "provider" | "id">) =>
    links.some(
        (link) =>
            link.kind === "KnowledgeBase" &&
            (link.provider ?? "").toLowerCase() === item.provider.toLowerCase() &&
            link.itemId === item.id
    );

// ── D&D Beyond (27e, step 22a's shapes) ──────────────────────────────────────

/**
 * The label a D&D Beyond sheet link carries. It is a **preset, not a kind**: the link that is
 * created is an ordinary `External` one, and nothing in the model knows about D&D Beyond.
 * Which is why the field recognises its own link by this label and the host, rather than by a
 * flag the API would have to keep.
 */
export const DND_BEYOND_LABEL = "D&D Beyond sheet";

/** The longest pasted text the field will even try to parse, as 22a's endpoint caps it. */
export const DND_BEYOND_INPUT_MAX = 500;

/** The hosts a sheet url may name. `ddb.ac` is D&D Beyond's own short-link host. */
const DND_BEYOND_HOSTS = ["dndbeyond.com", "www.dndbeyond.com", "ddb.ac", "www.ddb.ac"];

const isSheetId = (segment: string | undefined) => !!segment && /^[0-9]{1,12}$/.test(segment);

/**
 * A pasted D&D Beyond address as the **canonical** sheet url
 * `https://www.dndbeyond.com/characters/{id}`, or null when it is not one (step 22a's
 * `DndBeyondSheet.TryParse`, in the browser).
 *
 * Accepted over `http` or `https` (an `http` url is upgraded), with or without `www.`, with a
 * trailing slug, a trailing slash, a query or a fragment:
 *
 * - `https://www.dndbeyond.com/characters/{id}`
 * - `https://www.dndbeyond.com/characters/{id}/{slug}`
 * - `https://www.dndbeyond.com/profile/{user}/characters/{id}`
 * - `https://ddb.ac/characters/{id}/{slug}`
 *
 * Only the id survives, so a campaign or builder url, another host,
 * `dndbeyond.com.evil.test`, a user-info part, a 13-digit id and `javascript:` are all
 * refused, and what is stored is a url this app built rather than text a member pasted.
 */
export function dndBeyondSheetUrl(input: string | null | undefined): string | null {
    const text = (input ?? "").trim();
    if (!text || text.length > DND_BEYOND_INPUT_MAX) return null;
    let url: URL;
    try {
        url = new URL(text);
    } catch {
        return null;
    }
    if (url.protocol !== "https:" && url.protocol !== "http:") return null;
    if (url.username || url.password || url.port) return null;
    if (!DND_BEYOND_HOSTS.includes(url.hostname.toLowerCase())) return null;

    const segments = url.pathname.split("/").filter(Boolean);
    const first = segments[0]?.toLowerCase();
    // characters/{id} and characters/{id}/{slug}. Nothing deeper: /characters/{id}/builder/… is
    // the builder, not the sheet.
    if (first === "characters" && isSheetId(segments[1]) && segments.length <= 3) {
        return `https://www.dndbeyond.com/characters/${segments[1]}`;
    }
    // profile/{user}/characters/{id}[/{slug}].
    if (first === "profile" && segments[2]?.toLowerCase() === "characters" && isSheetId(segments[3]) && segments.length <= 5) {
        return `https://www.dndbeyond.com/characters/${segments[3]}`;
    }
    return null;
}

/** The field's message, in step 22a's words, or null when the address is a sheet. */
export function dndBeyondUrlError(input: string): string | null {
    if (!input.trim()) return "Paste the address of the character's sheet.";
    return dndBeyondSheetUrl(input)
        ? null
        : "That isn't a D&D Beyond character link. Copy it from the sheet's address bar or its Share button.";
}

/**
 * The entry's D&D Beyond sheet link, if it has one: an external link on a D&D Beyond host.
 * Recognised by the url rather than the label, because the label is the member's to change and
 * the host is not.
 */
export const dndBeyondLink = (links: readonly EntryLink[]): EntryLink | undefined =>
    links.find((link) => link.kind === "External" && dndBeyondSheetUrl(link.url) !== null);
