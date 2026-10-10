using System.Text.Json.Serialization;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record GetEntryHistoryRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
}

public record EntryHistoryResponse
{
    /// <summary>Every change the caller may see, oldest first.</summary>
    public required EntryHistoryItem[] Items { get; init; }
}

/// <summary>One change: when, by whom, and what.</summary>
public record EntryHistoryItem
{
    public required DateTimeOffset At { get; init; }
    public required Guid ActorMemberId { get; init; }
    public required EntryChange Change { get; init; }
}

/// <summary>What an event changed. <see cref="EntryChange.Type"/> says which of the other fields are set.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EntryChangeType>))]
public enum EntryChangeType
{
    /// <summary><see cref="EntryChange.Name"/>, <see cref="EntryChange.Kind"/>, <see cref="EntryChange.Visibility"/>, and <see cref="EntryChange.Source"/> when the caller may read it.</summary>
    Created,
    /// <summary><see cref="EntryChange.Name"/>.</summary>
    Renamed,
    /// <summary><see cref="EntryChange.Kind"/>.</summary>
    KindChanged,
    /// <summary><see cref="EntryChange.Alias"/>.</summary>
    AliasAdded,
    /// <summary><see cref="EntryChange.Alias"/>.</summary>
    AliasRemoved,
    /// <summary><see cref="EntryChange.Visibility"/>.</summary>
    VisibilityChanged,
    /// <summary><see cref="EntryChange.EditAccess"/>.</summary>
    EditAccessChanged,
    /// <summary><see cref="EntryChange.Blocks"/>: the whole article after the edit, as the caller sees it.</summary>
    ArticleEdited,
    /// <summary><see cref="EntryChange.Blocks"/>, as for an edit; the quote is the last block the caller sees.</summary>
    QuotePromoted,
    /// <summary>Another entry was merged into this one: <see cref="EntryChange.MergedEntryId"/>, <see cref="EntryChange.Name"/> (its name) and <see cref="EntryChange.Blocks"/>.</summary>
    Merged,
    /// <summary><see cref="EntryChange.MemberId"/>: the new claimer.</summary>
    Claimed,
    Unclaimed,
    /// <summary><see cref="EntryChange.Stats"/>: the stats after the change, null when cleared.</summary>
    StatsChanged,
    /// <summary><see cref="EntryChange.Link"/>: the link that was added, resolved as the caller reads it now (27b).</summary>
    LinkAdded,
    /// <summary><see cref="EntryChange.Link"/>: the link that was removed, as it was when it was added.</summary>
    LinkRemoved,
    /// <summary><see cref="EntryChange.Suggestion"/>: the knowledge-base row the entry was told it is not (28b).</summary>
    SuggestionDismissed,
    /// <summary><see cref="EntryChange.ImageId"/>: the entry's new primary image (SAM-12).</summary>
    PrimaryImageSet,
    /// <summary>The entry has no primary image again (SAM-12), by hand or because its image stopped being one everyone can see.</summary>
    PrimaryImageCleared,
}

/// <summary>
/// One change, as a tagged union flattened into one record (the generated types stay simple):
/// <see cref="Type"/> is the tag and names the fields that are set.
/// </summary>
public record EntryChange
{
    public required EntryChangeType Type { get; init; }
    public string? Name { get; init; }
    public EntryKind? Kind { get; init; }
    public string? Alias { get; init; }
    public Visibility? Visibility { get; init; }
    public EditAccess? EditAccess { get; init; }
    /// <summary>A version of the article: only the blocks the caller can see.</summary>
    public ArticleBlockResponse[]? Blocks { get; init; }
    public Guid? MergedEntryId { get; init; }
    public Guid? MemberId { get; init; }
    public StatsResponse? Stats { get; init; }
    /// <summary>
    /// The image a <c>PrimaryImageSet</c> chose (SAM-12). Shown to everyone who can see the entry:
    /// only an image everyone can see may be primary, so naming it reveals nothing.
    /// </summary>
    public Guid? ImageId { get; init; }
    /// <summary>The reference item an entry was made from (20b), on <c>Created</c> only, under <see cref="EntrySources"/>' rule.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EntrySourceResponse? Source { get; init; }
    /// <summary>
    /// The link an add or a remove was about (27b), under <see cref="EntryLinks"/>' rule — the same
    /// one. A removal names the link as the <c>EntryLinkAdded</c> earlier in the stream had it, with
    /// its knowledge-base row resolved as it is now, which is what lets history read "Removed a link
    /// · Beholder" rather than a bare id.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EntryLinkResponse? Link { get; init; }
    /// <summary>
    /// The knowledge-base row a dismissal was about (28b), resolved as the caller reads it now —
    /// under <see cref="EntryLinks"/>' rule, the same one the link rows use and for the same reason:
    /// "dismissed Beholder" on an unclaimed Character names the monster exactly as a link to it
    /// would, so a player sees no trace of the row rather than a redacted one.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EntrySuggestionResponse? Suggestion { get; init; }
}

/// <summary>
/// A dismissed knowledge-base suggestion as history names it (28b): "Beholder (5eTools)". Only the
/// key is stored on the event, so the name and the detail are read from the corpus on this request
/// and are null for a row that has since gone — then the item id is all history can say, which is
/// what the web falls back to.
/// </summary>
public record EntrySuggestionResponse
{
    /// <summary>The provider's key: <c>5etools</c>.</summary>
    public required string Provider { get; init; }
    /// <summary>What the UI calls that provider, or its key when the provider has gone from the app.</summary>
    public string? ProviderLabel { get; init; }
    /// <summary>The row's id within that provider: <c>monster_beholder_mm</c>.</summary>
    public required string ItemId { get; init; }
    /// <summary>The row's name now, "Beholder". Null when it has gone from the corpus.</summary>
    public string? Name { get; init; }
    /// <summary>The muted line, "CR 13 · Large Aberration · MM". Null when the row has gone.</summary>
    public string? Detail { get; init; }
}

/// <summary>
/// An entry's history (15g.4): every event on its stream, oldest first. Who can see the entry
/// may read it (a 404 otherwise; a merged id redirects to its target, like every read). It is
/// redacted per viewer, so nothing hidden leaves a trace (invariant 5):
/// <list type="bullet">
/// <item>An article version lists only the blocks the caller can see, and a version that
/// changed none of them is left out (<see cref="ArticleHistory.Changed"/>), timestamp and all.</item>
/// <item>A stats change is left out unless the caller could read the stats then: the entry was a
/// Character, and it was claimed or the caller is a DM (<see cref="EntryStats"/>).</item>
/// <item>The source on <c>Created</c> is there only when the caller may read it now
/// (<see cref="EntrySources"/>), which is exactly when <c>GET entry</c> shows it to them, so the
/// history never tells them more than the entry does.</item>
/// <item>A link added or removed (27b) is there only when the caller may read the entry's links now
/// (<see cref="EntryLinks"/>), the same "now" the source uses and for the same reason. So a player
/// reading an unclaimed Character's history sees no trace of the DM's link to Beholder, not a
/// greyed-out row (invariant 5), and a link on a Character that has since been unclaimed is hidden
/// from them again rather than handed back by the history.</item>
/// <item>A dismissed knowledge-base suggestion (28b) follows that same rule, because it is the same
/// disclosure: the row it names is a stat block by another route.</item>
/// </list>
/// Restoring a version is the web's job: it saves that version's blocks through <c>PUT
/// article</c> with the current etag, so the merge keeps the blocks the caller cannot see.
/// </summary>
public class GetEntryHistory(IDocumentSession session, ReferenceCatalog reference, EntryLinkResolver links)
    : Endpoint<GetEntryHistoryRequest, EntryHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/entries/{EntryId}/history");
    }

    public override async Task HandleAsync(GetEntryHistoryRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);

        var events = await session.Events.FetchStreamAsync(entry.Id, token: ct);
        // The source is looked up once, here, so For stays a plain iterator over the stream.
        var source = EntrySources.For(entry, member) is { } s ? await EntrySourceResponse.From(s, reference, ct) : null;
        await SendAsync(
            new EntryHistoryResponse
            {
                Items = For(
                    entry,
                    events,
                    member,
                    source,
                    await LinksOf(entry, member, events, ct),
                    await DismissalsOf(entry, member, events, ct)).ToArray(),
            },
            cancellation: ct);
    }

    /// <summary>
    /// Every link the stream ever added, by id, resolved — or nothing at all when
    /// <paramref name="viewer"/> may not read this entry's links, which is what filters both link
    /// rows out of their history.
    /// <para>
    /// <b>One</b> query for the whole history, for the reason <see cref="EntryLinkResolver"/> exists:
    /// a stream with twenty adds and twenty removes would otherwise be forty lookups. It is built
    /// from the <c>EntryLinkAdded</c> events rather than from <see cref="Entry.Links"/>, because a
    /// removed link is still in the history and no longer on the entry.
    /// </para>
    /// <para>
    /// <b>The rule is "may they read the links now", not "could they then"</b> — the source's rule
    /// (see the class summary), not the stats'. It has to be: a link added while a Character was
    /// claimed and then unclaimed is hidden from the players by <c>GET entry</c>, and a
    /// "could they then" history would hand it back to them. Asking the current entry also means
    /// history never shows a link the entry does not.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, EntryLinkResponse>> LinksOf(
        Entry entry, Member viewer, IReadOnlyList<JasperFx.Events.IEvent> events, CancellationToken ct)
    {
        var added = !EntryLinks.CanRead(entry, viewer)
            ? []
            : events.Select(e => e.Data).OfType<EntryLinkAdded>().Select(e => e.Link).ToList();
        if (added.Count == 0)
        {
            return new Dictionary<Guid, EntryLinkResponse>();
        }
        var items = await links.ResolveAsync(added, ct);
        return added.ToDictionary(link => link.Id, link => EntryLinkResponse.From(link, items.GetValueOrDefault(link.Id)));
    }

    /// <summary>
    /// Every knowledge-base row the stream ever dismissed (28b), by the key the event names it with,
    /// resolved — or nothing at all when <paramref name="viewer"/> may not read this entry's links,
    /// which is what filters the dismissal rows out of their history. It is <see cref="EntryLinks"/>'
    /// rule because it is the same disclosure: "dismissed Beholder" names the monster.
    /// <para>
    /// One query for every dismissal on the stream, beside <see cref="LinksOf"/>'s one for every link.
    /// Two statements rather than one because a link is resolved by its own id and a dismissal by its
    /// row's key, and sharing the statement would mean duplicating
    /// <see cref="EntryLinkResolver"/>'s key folding out here; two is still a constant, which is the
    /// property that matters.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<(string Provider, string ItemId), EntrySuggestionResponse>> DismissalsOf(
        Entry entry, Member viewer, IReadOnlyList<JasperFx.Events.IEvent> events, CancellationToken ct)
    {
        var dismissed = !EntryLinks.CanRead(entry, viewer)
            ? []
            : events.Select(e => e.Data).OfType<EntryKnowledgeBaseSuggestionDismissed>()
                .Select(e => (e.Provider, e.ItemId))
                .Distinct()
                .ToList();
        if (dismissed.Count == 0)
        {
            return new Dictionary<(string, string), EntrySuggestionResponse>();
        }

        var items = await links.ResolveItemsAsync(dismissed, ct);
        return dismissed.ToDictionary(
            key => key,
            key =>
            {
                var item = items.GetValueOrDefault(key);
                return new EntrySuggestionResponse
                {
                    Provider = key.Provider,
                    ProviderLabel = item?.ProviderLabel ?? key.Provider,
                    ItemId = key.ItemId,
                    Name = item?.Name,
                    Detail = item?.Detail,
                };
            });
    }

    /// <summary>
    /// The history of <paramref name="entry"/> (its whole stream, <paramref name="events"/>) as
    /// <paramref name="viewer"/> may read it. <paramref name="source"/> is the entry's reference
    /// source as they may read it, or null when there is none or it is not theirs to see.
    /// <paramref name="links"/> holds every link the stream added, resolved, by id.
    /// </summary>
    public static IEnumerable<EntryHistoryItem> For(
        Entry entry,
        IReadOnlyList<JasperFx.Events.IEvent> events,
        Member viewer,
        EntrySourceResponse? source,
        IReadOnlyDictionary<Guid, EntryLinkResponse> links,
        IReadOnlyDictionary<(string Provider, string ItemId), EntrySuggestionResponse> dismissals)
    {
        // Every version of the article, with the index of the event that made it.
        var versions = new List<(int EventIndex, IReadOnlyList<ArticleBlock> Blocks)>();
        IReadOnlyList<ArticleBlock> blocks = [];
        for (var i = 0; i < events.Count; i++)
        {
            IReadOnlyList<ArticleBlock>? next = events[i].Data switch
            {
                EntryArticleEdited e => e.Blocks,
                EntryQuotePromoted e => [.. blocks, e.Block],
                EntryAbsorbed e => [.. blocks, EntryMerge.Heading(e), .. e.FromBlocks],
                _ => null,
            };
            if (next is not null)
            {
                blocks = next;
                versions.Add((i, next));
            }
        }
        var visibleVersions = ArticleHistory.Changed(entry, versions.Select(v => v.Blocks), viewer)
            .ToDictionary(c => versions[c.Index].EventIndex, c => c.Blocks);

        // The kind and the claim as they were, for the stats read rule at each change.
        var kind = entry.Kind;
        Guid? claimer = null;
        for (var i = 0; i < events.Count; i++)
        {
            var @event = events[i];
            switch (@event.Data)
            {
                case EntryCreated e: kind = e.Kind; break;
                case EntryKindChanged e: kind = e.Kind; break;
                case EntryClaimed e: claimer = e.MemberId; break;
                case EntryUnclaimed: claimer = null; break;
                case EntryAbsorbed e: claimer ??= e.FromClaimedByMemberId; break;
            }

            var visible = visibleVersions.GetValueOrDefault(i);
            EntryChange? change = @event.Data switch
            {
                EntryCreated e => new()
                {
                    Type = EntryChangeType.Created,
                    Name = e.Name,
                    Kind = e.Kind,
                    Visibility = e.Visibility,
                    Source = e.Source is null ? null : source,
                },
                EntryRenamed e => new() { Type = EntryChangeType.Renamed, Name = e.Name },
                EntryKindChanged e => new() { Type = EntryChangeType.KindChanged, Kind = e.Kind },
                EntryAliasAdded e => new() { Type = EntryChangeType.AliasAdded, Alias = e.Alias },
                EntryAliasRemoved e => new() { Type = EntryChangeType.AliasRemoved, Alias = e.Alias },
                EntryVisibilityChanged e => new() { Type = EntryChangeType.VisibilityChanged, Visibility = e.Visibility },
                EntryEditAccessChanged e => new() { Type = EntryChangeType.EditAccessChanged, EditAccess = e.EditAccess },
                EntryArticleEdited when visible is not null => new() { Type = EntryChangeType.ArticleEdited, Blocks = Blocks(visible) },
                EntryQuotePromoted when visible is not null => new() { Type = EntryChangeType.QuotePromoted, Blocks = Blocks(visible) },
                // The heading is an ordinary block, so everyone who sees the entry sees the
                // merge; they could all see the merged entry when it happened (the merge guard).
                EntryAbsorbed e => new()
                {
                    Type = EntryChangeType.Merged,
                    MergedEntryId = e.FromEntryId,
                    Name = e.FromName,
                    Blocks = visible is null ? null : Blocks(visible),
                },
                EntryClaimed e => new() { Type = EntryChangeType.Claimed, MemberId = e.MemberId },
                EntryUnclaimed => new() { Type = EntryChangeType.Unclaimed },
                EntryStatsChanged e when CouldReadStats(kind, claimer, viewer) => new()
                {
                    Type = EntryChangeType.StatsChanged,
                    Stats = e.Stats is null ? null : StatsResponse.From(e.Stats),
                },
                // The link rows. `links` is empty when the caller may not read this entry's links, so
                // the lookup is the filter; see LinksOf for why the rule is "now" and not "then".
                EntryLinkAdded e when links.TryGetValue(e.Link.Id, out var added)
                    => new() { Type = EntryChangeType.LinkAdded, Link = added },
                EntryLinkRemoved e when links.TryGetValue(e.LinkId, out var removed)
                    => new() { Type = EntryChangeType.LinkRemoved, Link = removed },
                // And the dismissal rows, filtered the same way: `dismissals` is empty when the
                // caller may not read this entry's links (28b, DismissalsOf).
                EntryKnowledgeBaseSuggestionDismissed e when dismissals.TryGetValue((e.Provider, e.ItemId), out var dismissed)
                    => new() { Type = EntryChangeType.SuggestionDismissed, Suggestion = dismissed },
                // The primary image rows (SAM-12) need no rule: the image was one everyone could
                // see when it was chosen, so its id is no more private than the entry's name.
                EntryPrimaryImageSet e => new() { Type = EntryChangeType.PrimaryImageSet, ImageId = e.ImageId },
                EntryPrimaryImageCleared => new() { Type = EntryChangeType.PrimaryImageCleared },
                _ => null,
            };
            if (change is not null)
            {
                yield return new EntryHistoryItem
                {
                    At = @event.Timestamp,
                    ActorMemberId = ((IActorEvent)@event.Data).Actor.MemberId,
                    Change = change,
                };
            }
        }
    }

    private static bool CouldReadStats(EntryKind kind, Guid? claimer, Member viewer)
        => kind == EntryKind.Character && (claimer is not null || viewer.Role == Role.DM);


    private static ArticleBlockResponse[] Blocks(IReadOnlyList<ArticleBlock> blocks)
        => blocks.Select(ArticleBlockResponse.From).ToArray();
}
