using System.Text.Json.Serialization;

using TakeInitiative.KnowledgeBase.Store;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// What a link points at (§11, 27b). The two kinds are one list with one shape, so a third
/// knowledge base or a third site is a new case here and nothing else.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EntryLinkKind>))]
public enum EntryLinkKind
{
    /// <summary>A row in the knowledge base (step 26): <c>Provider</c> and <c>ItemId</c> are set, <c>Url</c> and <c>Label</c> are not.</summary>
    KnowledgeBase,
    /// <summary>A URL the member typed: <c>Url</c> and <c>Label</c> are set, <c>Provider</c> and <c>ItemId</c> are not.</summary>
    External,
}

/// <summary>
/// One link on an entry (glossary: Link, 27b).
/// <para>
/// A knowledge-base link <b>stores no copy of the row</b>. Its name, detail, book, page, url,
/// artwork and <c>stale</c> flag are resolved on every read by <see cref="EntryLinkResolver"/>.
/// That is the whole reason the corpus moved into Postgres in step 26: a link is a foreign key,
/// so a re-ingest that corrects a page number or a url fixes every link to it at once. Copying
/// the row onto the link would save a query and make that impossible.
/// </para>
/// </summary>
/// <param name="Id">Generated when the link is added, so <c>DELETE</c> has something to name.</param>
/// <param name="Kind">Which of the two shapes this is; see <see cref="EntryLinkKind"/>.</param>
/// <param name="Provider">A <c>IReferenceProvider.Key</c> (<c>5etools</c>) for a knowledge-base link; null for an external one.</param>
/// <param name="ItemId">The row's id within that provider (<c>monster_beholder_mm</c>); null for an external link.</param>
/// <param name="Url">The absolute <c>http</c> or <c>https</c> url of an external link; null for a knowledge-base one, whose url comes from the corpus.</param>
/// <param name="Label">What the member called an external link. Never rendered as markdown. Null for a knowledge-base link, which has no label of its own.</param>
/// <param name="AddedAt">When it was added. <see cref="Entry.Links"/> is ordered by it.</param>
/// <param name="AddedByMemberId">Who added it. The event carries the <c>Actor</c> too; this is here so a read does not need the stream.</param>
public sealed record EntryLink(
    Guid Id,
    EntryLinkKind Kind,
    string? Provider,
    string? ItemId,
    string? Url,
    string? Label,
    DateTimeOffset AddedAt,
    Guid AddedByMemberId);

/// <summary>
/// Who reads an entry's <see cref="Entry.Links"/> (27b): <b>exactly</b>
/// <see cref="EntrySources.CanRead"/>, for both kinds, and deliberately not
/// <see cref="EntryVisibility.CanSee"/>.
/// <para>
/// <b>Why the same rule.</b> <see cref="EntrySources"/> exists because an NPC's source is its
/// stat block, so "Mysterious Stranger ← SRD Vampire" must not reach a player (invariant 8). A
/// knowledge-base link is that same disclosure by a different route: "The Eye ↗ Beholder" gives
/// the monster away just as completely. An external link does too — "The Eye ↗
/// dndbeyond.com/monsters/vampire" is a combat secret with a url in front of it — so the rule is
/// applied to external links as well, which is correct rather than merely convenient:
/// </para>
/// <list type="bullet">
/// <item>A claimed Character (a player character): everyone who can see the entry, which is the
/// D&amp;D Beyond sheet case — the player wants their sheet shared.</item>
/// <item>An unclaimed Character (an NPC or a monster): the DMs only.</item>
/// <item>Place, Faction, Item, Event: everyone who can see the entry. Nothing is being hidden,
/// and the entry's own visibility is the gate.</item>
/// </list>
/// <para>
/// <b>Do not "simplify" this to <see cref="EntryVisibility.CanSee"/>.</b> It looks like a
/// redundant indirection and it is not: every NPC's stat block would leak through its links the
/// moment it changed. There is no per-link visibility toggle either, on purpose — one rule,
/// already written and already tested, is the whole point of reusing it.
/// </para>
/// </summary>
public static class EntryLinks
{
    /// <summary>How many links one entry may carry. A 21st is a 409.</summary>
    public const int MaxPerEntry = 20;

    /// <summary>The longest external url, in characters.</summary>
    public const int UrlMaxLength = 2048;

    /// <summary>The longest label on an external link, after trimming.</summary>
    public const int LabelMaxLength = 80;

    /// <summary>See the class summary: this is <see cref="EntrySources.CanRead"/>, and it must stay so.</summary>
    public static bool CanRead(Entry entry, Member viewer) => EntrySources.CanRead(entry, viewer);

    /// <summary>
    /// Who may add or remove a link (27d): whoever may edit the entry
    /// (<see cref="EntryPermissions.CanEdit"/>, invariant 4), <b>plus the member who plays a claimed
    /// Character</b>.
    /// <para>
    /// The second clause is not a widening for its own sake. <see cref="EntryPermissions.CanEdit"/>
    /// is <c>DM || creator || (EditAccess == Anyone &amp;&amp; CanSee)</c>, with no claimer clause at
    /// all, so it only appears to cover a player's own character because <c>Anyone</c> is the
    /// default: a DM who creates an NPC, hands it to a player and then sets edit access to
    /// <c>OnlyMe</c> would lock that player out of their own character's D&amp;D Beyond sheet link,
    /// which is the whole of 27e.
    /// </para>
    /// <para>
    /// The shape is <see cref="EntryStats.CanWrite"/>'s, deliberately: "the player who plays this
    /// character may maintain its links" is expressed the way "…may maintain its stats" already is,
    /// rather than invented. It is <b>only</b> about links — the article, the name and the kind keep
    /// <see cref="EntryPermissions.CanEdit"/>, so a claimer whose edit access was restricted can
    /// still not rewrite the entry.
    /// </para>
    /// <para>
    /// <b>This deliberately does not require <see cref="CanRead"/>, and adding that breaks a test
    /// on purpose.</b> On one shape — an unclaimed Character whose edit access is <c>Anyone</c> — a
    /// plain member may write links they cannot read. That looks like a bug and the obvious fix is
    /// <c>CanRead(entry, viewer) &amp;&amp; …</c>. It is the wrong fix: <c>DELETE</c> answers
    /// <b>404</b> for a link the caller may not read, precisely so that naming one does not confirm
    /// it exists, and an authorisation check that fails first turns that 404 into a 403 — which
    /// discloses the link. Invariant 5 applies to status codes too, and this is the one place in the
    /// links feature where it bites. <c>DeletingAnUnknownLink_IsA404_AndSoIsOneTheCallerMayNotRead</c>
    /// is the test that catches it. The write-then-invisible case is a usability wart, not a leak,
    /// and the web gates its own control on read ∧ write so nobody reaches it.
    /// </para>
    /// </summary>
    public static bool CanWrite(Entry entry, Member viewer)
        => EntryPermissions.CanEdit(entry, viewer)
            || (entry.Kind == EntryKind.Character
                && entry.ClaimedByMemberId is { } claimer
                && claimer == viewer.MemberId
                && EntryVisibility.CanSee(entry, viewer));

    /// <summary>
    /// The links as <paramref name="viewer"/> may read them, in <see cref="EntryLink.AddedAt"/>
    /// order: empty when there are none or they may not, so "none" and "not for you" look the same.
    /// </summary>
    public static IReadOnlyList<EntryLink> For(Entry entry, Member viewer)
        => CanRead(entry, viewer) ? entry.Links : [];

    /// <summary>
    /// The knowledge-base rows <paramref name="links"/> point at, as
    /// <see cref="EntryKnowledgeBaseLinks.Key"/> writes them for the prune's GIN query. An
    /// external link contributes nothing, and a knowledge-base link missing either half of its
    /// key — which no writer can produce — is left out rather than keyed as half of one.
    /// </summary>
    public static IEnumerable<string> ItemKeys(IEnumerable<EntryLink> links)
    {
        foreach (var link in links)
        {
            if (link is { Kind: EntryLinkKind.KnowledgeBase, Provider: { } provider, ItemId: { } itemId })
            {
                yield return EntryKnowledgeBaseLinks.Key(provider, itemId);
            }
        }
    }
}

/// <summary>
/// The rules an external link's url has to pass, in one place because the validator (27c) and the
/// duplicate check both need them and they must agree.
/// </summary>
public static class EntryLinkUrl
{
    /// <summary>
    /// Parsed and checked: absolute, <c>http</c> or <c>https</c>, and no longer than
    /// <see cref="EntryLinks.UrlMaxLength"/>. Null when it is none of those.
    /// <para>
    /// This is what rejects <c>javascript:</c>, <c>data:</c> and <c>file:</c> — by naming the two
    /// schemes that are allowed rather than the ones that are not, so a scheme nobody thought of is
    /// refused as well. It happens here, in the model the validator calls, and not in the view: a
    /// url that cannot be stored can never be rendered wrongly later.
    /// </para>
    /// </summary>
    public static Uri? Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }
        var trimmed = url.Trim();
        if (trimmed.Length > EntryLinks.UrlMaxLength)
        {
            return null;
        }
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }

    /// <summary>True when <paramref name="url"/> is one this app will store.</summary>
    public static bool IsAllowed(string? url) => Parse(url) is not null;

    /// <summary>
    /// The form two urls are compared in for the duplicate check (27c): the scheme and host folded
    /// to lower case and a single trailing slash on the path dropped, with the query and the
    /// fragment left exactly as typed. Case folds because a host is case-insensitive; a path does
    /// not, because many sites' paths are. Null for a url <see cref="Parse"/> refuses.
    /// </summary>
    public static string? Normalize(string? url)
    {
        if (Parse(url) is not { } uri)
        {
            return null;
        }
        var path = uri.AbsolutePath.Length > 1 ? uri.AbsolutePath.TrimEnd('/') : uri.AbsolutePath;
        return $"{uri.Scheme.ToLowerInvariant()}://{uri.Authority.ToLowerInvariant()}{path}{uri.Query}{uri.Fragment}";
    }
}
