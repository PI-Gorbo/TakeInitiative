using Marten.Events;

using TakeInitiative.KnowledgeBase.Store;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// Inline projection of an Entry stream (stream id = entry id). The article is from 15e,
/// merge, claim and stats from 15g, <see cref="Source"/> from 20b and <see cref="Links"/>
/// from 27b. The two were the §11 seams: declared in step 15 so that later steps add events
/// rather than a migration, which is what both of them did.
/// </summary>
public record Entry
{
    public Guid Id { get; init; }
    public Guid CampaignId { get; init; }
    public Guid CreatorMemberId { get; init; }
    public string Name { get; init; } = "";
    public EntryKind Kind { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public Visibility Visibility { get; init; }
    public EditAccess EditAccess { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public Guid? CreatedFromNoteId { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// The reference item the entry was made from by + Wiki (20b), or null. Set once, at creation.
    /// Read through <see cref="EntrySources"/>, never as is: an NPC's source is its stat block.
    /// </summary>
    public EntrySource? Source { get; init; }
    /// <summary>
    /// The entry's links (27b), oldest first. Read through <see cref="EntryLinks"/>, never as is:
    /// an NPC's link to a monster is its stat block, exactly as its <see cref="Source"/> is.
    /// </summary>
    public IReadOnlyList<EntryLink> Links { get; init; } = [];
    /// <summary>
    /// One <c>provider:id</c> key per knowledge-base link (<see cref="EntryLinks.ItemKeys"/>),
    /// derived from <see cref="Links"/> the way <see cref="ArticleMentionIds"/> is derived from the
    /// article, and GIN-indexed for one question: "does any entry link to this row?", which 26c's
    /// prune asks before it deletes anything (<see cref="EntryKnowledgeBaseLinks"/>). It is an
    /// index, not state: nothing reads it but that query.
    /// </summary>
    public string[] LinkedItemKeys { get; init; } = [];

    /// <summary>
    /// Every block, secret ones included. Never sent as is: reads go through
    /// <see cref="ArticleView"/>, which keeps only the blocks the viewer can see.
    /// </summary>
    public Article Article { get; init; } = new();
    /// <summary>
    /// The entries any block mentions (<see cref="MentionParser.EntryIds"/> over the blocks in
    /// order), for <see cref="MentionIndex"/>. It has a GIN index. Each block's own visibility
    /// is applied when the index is read.
    /// </summary>
    public Guid[] ArticleMentionIds { get; init; } = [];

    /// <summary>
    /// Set when this entry was merged into another (15g). Its id then redirects there, and
    /// every list leaves it out. It is never cleared: there is no un-merge.
    /// </summary>
    public Guid? MergedIntoId { get; init; }
    /// <summary>
    /// Every entry merged into this one, directly or along a chain (A into B, then B into C
    /// gives C both). Mentions of these ids resolve to this entry (invariant 6: no text is
    /// rewritten), so <see cref="MentionIndex"/> asks for <see cref="MentionIds"/>.
    /// </summary>
    public Guid[] MergedFromIds { get; init; } = [];
    /// <summary>The member whose player character this is (glossary: Claim). Only a Character entry has one.</summary>
    public Guid? ClaimedByMemberId { get; init; }
    /// <summary>The stat line (glossary: Stats). Read through <see cref="EntryStats"/>, never as is.</summary>
    public Stats? Stats { get; init; }

    /// <summary>The ids whose mentions mean this entry: its own and every merged one.</summary>
    public Guid[] MentionIds() => [Id, .. MergedFromIds];

    public const int NameMaxLength = 100;
    public const int MaxAliases = 20;

    public static Entry Create(IEvent<EntryCreated> @event)
    {
        var e = @event.Data;
        return new Entry
        {
            Id = @event.StreamId,
            CampaignId = e.CampaignId,
            CreatorMemberId = e.CreatorMemberId,
            Name = e.Name,
            Kind = e.Kind,
            Visibility = e.Visibility,
            EditAccess = EditAccess.Anyone,
            CreatedAt = @event.Timestamp,
            CreatedFromNoteId = e.CreatedFromNoteId,
            Source = e.Source,
            UpdatedAt = @event.Timestamp,
        };
    }

    public Entry Apply(IEvent<EntryRenamed> @event) => this with { Name = @event.Data.Name, UpdatedAt = @event.Timestamp };

    public Entry Apply(IEvent<EntryKindChanged> @event) => this with { Kind = @event.Data.Kind, UpdatedAt = @event.Timestamp };

    public Entry Apply(IEvent<EntryAliasAdded> @event)
        => this with { Aliases = [.. Aliases, @event.Data.Alias], UpdatedAt = @event.Timestamp };

    public Entry Apply(IEvent<EntryAliasRemoved> @event)
        => this with { Aliases = Aliases.Where(a => a != @event.Data.Alias).ToList(), UpdatedAt = @event.Timestamp };

    public Entry Apply(IEvent<EntryVisibilityChanged> @event)
        => this with { Visibility = @event.Data.Visibility, UpdatedAt = @event.Timestamp };

    public Entry Apply(IEvent<EntryEditAccessChanged> @event)
        => this with { EditAccess = @event.Data.EditAccess, UpdatedAt = @event.Timestamp };

    public Entry Apply(IEvent<EntryMerged> @event)
        => this with { MergedIntoId = @event.Data.IntoEntryId, UpdatedAt = @event.Timestamp };

    public Entry Apply(IEvent<EntryAbsorbed> @event)
    {
        var e = @event.Data;
        return WithBlocks([.. Article.Blocks, EntryMerge.Heading(e), .. e.FromBlocks]) with
        {
            Aliases = EntryNameRules.NormalizeAliases([.. Aliases, e.FromName, .. e.FromAliases], Name),
            MergedFromIds = MergedFromIds.Append(e.FromEntryId).Concat(e.FromMergedIds).Where(id => id != Id).Distinct().ToArray(),
            ClaimedByMemberId = ClaimedByMemberId ?? e.FromClaimedByMemberId,
            Stats = Stats ?? e.Stats,
            UpdatedAt = @event.Timestamp,
        };
    }

    public Entry Apply(IEvent<EntryClaimed> @event)
        => this with { ClaimedByMemberId = @event.Data.MemberId, UpdatedAt = @event.Timestamp };

    public Entry Apply(IEvent<EntryUnclaimed> @event)
        => this with { ClaimedByMemberId = null, UpdatedAt = @event.Timestamp };

    // Stats leave UpdatedAt alone: on an unclaimed entry only the DMs may read them, and
    // UpdatedAt is on the summary everyone who sees the entry receives.
    public Entry Apply(EntryStatsChanged e) => this with { Stats = e.Stats };

    // Article events leave UpdatedAt alone. It is on the summary every member who can see
    // the entry receives, so moving it on an edit inside a secret block would tell them that
    // the secret exists (invariant 5).
    public Entry Apply(EntryArticleEdited e) => WithBlocks(e.Blocks);

    public Entry Apply(EntryQuotePromoted e) => WithBlocks([.. Article.Blocks, e.Block]);

    // Link events leave UpdatedAt alone, for the reason stats do: on an unclaimed Character only
    // the DMs may read the links (EntryLinks), and UpdatedAt is on the summary every member who
    // can see the entry receives, so moving it would tell them a link they cannot see exists
    // (invariant 5).
    public Entry Apply(EntryLinkAdded e) => WithLinks([.. Links, e.Link]);

    // Remove by id, and silently so: a removal of a link that is already gone is not a conflict.
    // The endpoint is what turns an unknown id into a 404, before it appends anything.
    public Entry Apply(EntryLinkRemoved e) => WithLinks([.. Links.Where(l => l.Id != e.LinkId)]);

    private Entry WithBlocks(IReadOnlyList<ArticleBlock> blocks) => this with
    {
        Article = new Article { Blocks = blocks },
        ArticleMentionIds = blocks.SelectMany(b => MentionParser.EntryIds(b.Text)).Distinct().ToArray(),
    };

    /// <summary>
    /// The links, ordered by <see cref="EntryLink.AddedAt"/> (the order is part of what
    /// <see cref="Links"/> promises, so it is applied here rather than per reader), and the
    /// derived key array that carries the prune's GIN index. <c>OrderBy</c> is stable, so two
    /// links added in the same microsecond keep the order they were appended in.
    /// </summary>
    private Entry WithLinks(IReadOnlyList<EntryLink> links)
    {
        var ordered = links.OrderBy(l => l.AddedAt).ToList();
        return this with
        {
            Links = ordered,
            LinkedItemKeys = [.. EntryLinks.ItemKeys(ordered).Distinct(StringComparer.Ordinal)],
        };
    }
}

/// <summary>
/// §11: where an entry came from. From 20b, a reference item: <see cref="Provider"/> is the
/// provider's key (<c>srd52</c>), <see cref="ExternalId"/> the item's id and <see cref="Url"/> a
/// public page for it (the SRD's own page for SRD items). The web links to the app's card through
/// the provider and the id, not through the url. Later, a D&amp;D Beyond sheet or an imported message.
/// </summary>
public record EntrySource(string Provider, string ExternalId, string Url);
