// The entry page's collapsed sections on a phone (25e): the one-line peeks a closed
// section shows (stats, Details' access, gallery, combats) and the header's truncated
// aliases. Pure, so the page and `EntrySection` only wire them to the DOM.
import type { Entry, EntrySummary } from "./api/types";
import { aliasesLabel, editAccessLabel } from "./entries";
import { imageCountLabel } from "./gallery";

/** How many aliases the header shows before "+N more". */
export const ALIASES_SHOWN = 3;

/**
 * The header's "aka …" line: the first `limit` aliases, and how many more a tap reveals.
 * Expanded, every alias shows and nothing is hidden.
 */
export function aliasPreview(
    aliases: readonly string[],
    expanded = false,
    limit = ALIASES_SHOWN
): { label: string; hidden: number } {
    const shown = expanded ? aliases : aliases.slice(0, Math.max(0, limit));
    return { label: aliasesLabel(shown), hidden: aliases.length - shown.length };
}

/**
 * An initiative roll as the peek's modifier: "1d20+3" → "+3", "1d20" → "+0". Anything
 * else (advantage, "2d20kh1+1", a flat number) is shown as written.
 */
export function initiativeModifier(roll: string): string {
    const match = /^1?d20\s*(?:([+-])\s*(\d+))?$/i.exec(roll.trim());
    if (!match) return roll.trim();
    return match[1] ? `${match[1]}${match[2]}` : "+0";
}

/** The stats peek, most useful first: "AC 15 · HP 38 · Init +3", or "" with no stats. */
export function statsPeekLabel(stats: Entry["stats"]): string {
    if (!stats) return "";
    return [
        stats.ac != null ? `AC ${stats.ac}` : "",
        stats.maxHp ? `HP ${stats.maxHp.trim()}` : "",
        stats.initiativeRoll ? `Init ${initiativeModifier(stats.initiativeRoll)}` : "",
    ]
        .filter(Boolean)
        .join(" · ");
}

/** Who can see an entry, in Details' words: "Everyone", "🔒 The DMs and Sam", "🔒 Only you". */
export function visibilityText(
    entry: Pick<EntrySummary, "visibility" | "creatorMemberId">,
    viewerMemberId: string,
    creatorName: string
): string {
    const creator = entry.creatorMemberId === viewerMemberId ? "you" : creatorName;
    switch (entry.visibility) {
        case "Everyone":
            return "Everyone";
        case "DM":
            return `🔒 The DMs and ${creator}`;
        default:
            return `🔒 Only ${creator}`;
    }
}

/** Details' peek: "Everyone can see · Anyone can edit", "🔒 DMs can see · Only Sam can edit". */
export function accessPeekLabel(
    entry: Pick<EntrySummary, "visibility" | "creatorMemberId" | "editAccess">,
    viewerMemberId: string,
    creatorName: string
): string {
    const see =
        entry.visibility === "Everyone"
            ? "Everyone can see"
            : entry.visibility === "DM"
              ? "🔒 DMs can see"
              : `🔒 Only ${entry.creatorMemberId === viewerMemberId ? "you" : creatorName} can see`;
    const edit = editAccessLabel(entry, viewerMemberId, creatorName).replace(/^Only me$/, "Only you");
    return `${see} · ${edit} can edit`;
}

/** Gallery's peek: "No images", "1 image", "4 images"; "" while it loads. */
export function galleryPeekLabel(count: number | undefined): string {
    if (count === undefined) return "";
    return count === 0 ? "No images" : imageCountLabel(count);
}

/** Combats' peek: "No combats", "In 1 combat", "In 2 combats"; "" while it loads. */
export function combatsPeekLabel(count: number | undefined): string {
    if (count === undefined) return "";
    if (count === 0) return "No combats";
    return `In ${count} ${count === 1 ? "combat" : "combats"}`;
}
