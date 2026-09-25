using Marten.Events;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// Inline projection of an Entry stream (stream id = entry id). The article is from 15e,
/// and merge, claim and stats arrive in 15g. <see cref="Source"/> and <see cref="Links"/> are
/// the §11 seams: declared now so steps 20–22 add events rather than a migration, never
/// written in step 15 and left out of every response.
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

    /// <summary>§11 seam: where the entry came from. Always null in step 15.</summary>
    public EntrySource? Source { get; init; }
    /// <summary>§11 seam. Always empty in step 15.</summary>
    public IReadOnlyList<EntryLink> Links { get; init; } = [];

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

    // Article events leave UpdatedAt alone. It is on the summary every member who can see
    // the entry receives, so moving it on an edit inside a secret block would tell them that
    // the secret exists (invariant 5).
    public Entry Apply(EntryArticleEdited e) => WithBlocks(e.Blocks);

    public Entry Apply(EntryQuotePromoted e) => WithBlocks([.. Article.Blocks, e.Block]);

    private Entry WithBlocks(IReadOnlyList<ArticleBlock> blocks) => this with
    {
        Article = new Article { Blocks = blocks },
        ArticleMentionIds = blocks.SelectMany(b => MentionParser.EntryIds(b.Text)).Distinct().ToArray(),
    };
}

/// <summary>§11: a reference item, a D&amp;D Beyond sheet or an imported message. Unused in step 15.</summary>
public record EntrySource(string Provider, string ExternalId, string Url);

/// <summary>§11: an outside link on an entry. Unused in step 15.</summary>
public record EntryLink(string Url, string? Label);
