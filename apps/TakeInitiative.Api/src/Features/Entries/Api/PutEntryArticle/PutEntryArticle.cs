using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Marten.Exceptions;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// One block of the caller's view of the article after their edit. <see cref="Id"/> is an
/// existing block the caller can see, or null for a new block that the caller will own. A
/// quote's source cannot be set or changed here; it is kept from the stored block.
/// </summary>
public record ArticleBlockRequest
{
    public Guid? Id { get; init; }
    public required string Text { get; init; }
    public required Visibility Visibility { get; init; }
}

public record PutEntryArticleRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>The <c>article.etag</c> the caller's edit started from.</summary>
    public required string Etag { get; init; }
    /// <summary>The caller's whole view of the article after the edit, in order.</summary>
    public required ArticleBlockRequest[] Blocks { get; init; }
    /// <summary>Entries to create with the edit, each already mentioned in a block (see <see cref="NewEntries"/>).</summary>
    public NewEntryRequest[]? NewEntries { get; init; }
}

public class PutEntryArticleRequestValidator : Validator<PutEntryArticleRequest>
{
    public PutEntryArticleRequestValidator()
    {
        RuleFor(x => x.Etag).NotNull();
        RuleFor(x => x.Blocks).NotNull();
        RuleFor(x => x.Blocks)
            .Must(blocks => blocks.Length <= Article.MaxBlocks)
            .WithMessage($"An article can have at most {Article.MaxBlocks} blocks.")
            .Must(blocks => blocks.Sum(b => (b.Text ?? "").Trim().Length) <= Article.MaxCharacters)
            .WithMessage($"An article can be at most {Article.MaxCharacters:N0} characters long.")
            .When(x => x.Blocks is not null);
        RuleForEach(x => x.Blocks).ChildRules(block =>
        {
            block.RuleFor(b => b.Text).NotNull();
            block.RuleFor(b => b.Visibility).IsInEnum();
        });
        RuleFor(x => x.NewEntries).NewEntriesList("An article edit");
    }
}

/// <summary>
/// Saves the caller's view of an article (15e.3 and 15e.4). The view is merged with the
/// blocks the caller cannot see (<see cref="ArticleMerge"/>), which stay in place.
/// <list type="bullet">
/// <item>An <c>etag</c> that is not the caller's current view (<see cref="ArticleEtag"/>) is a
/// 409 with <c>errors.etag</c>. The etag covers only what the caller can see, so a change to
/// a block they cannot see never gives them a 409.</item>
/// <item>An unknown or unseen block id is a 400 (<c>errors.blocks</c>), and a visibility
/// change by someone other than the block's owner or a DM is a 403.</item>
/// <item>The append is checked against the stream version. When another write wins the race,
/// the edit is checked and merged again once, and it is a 409 only if the caller's view
/// really changed.</item>
/// <item><c>newEntries</c> are created as with a note, with the narrowest visibility of the
/// entry and the blocks that mention them, and the caller as creator.</item>
/// </list>
/// Nothing is appended or pushed when nothing changed. Otherwise the new entries are pushed
/// (<c>entryUpserted</c>), then <c>entryArticleChanged</c> goes to the members whose view
/// changed. The response is the entry as the caller sees it.
/// </summary>
public class PutEntryArticle(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutEntryArticleRequest, EntryResponse>
{
    public const string EtagErrorKey = "etag";
    public const string BlocksErrorKey = "blocks";
    public const string ConflictMessage = "Someone else changed this article. Reload and re-apply.";

    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/entries/{EntryId}/article");
    }

    public override async Task HandleAsync(PutEntryArticleRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCanEdit(entry, member);

        var edits = req.Blocks.Select(b => new ArticleBlockEdit(b.Id, b.Text, b.Visibility)).ToList();
        var text = string.Join("\n\n", edits.Select(e => e.Text));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var (before, after, newEntryIds) = await Save(req, edits, text, member, ct);
                await hub.NotifyCreated(session, newEntryIds, ct);
                if (!ReferenceEquals(before, after))
                {
                    await hub.NotifyEntryArticleChanged(campaign.Members, before, after);
                }
                await SendAsync(EntryResponse.From(after, member), cancellation: ct);
                return;
            }
            catch (ConcurrencyException) when (attempt == 1)
            {
                // Another write to this entry landed between our read and our append. Drop
                // what we staged and do it all again against the new version.
                session.EjectAllPendingChanges();
            }
            catch (ConcurrencyException)
            {
                Conflict();
            }
        }
    }

    /// <summary>
    /// One try: read the entry at its current version, check the etag, merge and append. It
    /// returns the same instance as <c>before</c> and <c>after</c> when nothing was appended
    /// to the article.
    /// </summary>
    private async Task<(Entry Before, Entry After, IReadOnlyList<Guid> NewEntryIds)> Save(
        PutEntryArticleRequest req, IReadOnlyList<ArticleBlockEdit> edits, string text, Member member, CancellationToken ct)
    {
        var stream = await session.Events.FetchForWriting<Entry>(req.EntryId, ct);
        var current = stream.Aggregate;
        // The entry may have changed since it was checked: check again at this version.
        if (current is null || current.CampaignId != req.CampaignId || !EntryVisibility.CanSee(current, member))
        {
            ThrowError("There is no entry with the given id.", StatusCodes.Status404NotFound);
        }
        this.RequireCanEdit(current, member);

        if (ArticleEtag.For(current, member) != req.Etag)
        {
            Conflict();
        }

        var merged = ArticleMerge.Merge(current, current.Article.Blocks, edits, member);
        switch (merged.Error)
        {
            case ArticleMergeError.UnknownBlock:
                ThrowError(new ValidationFailure(BlocksErrorKey, $"There is no block with the id {merged.BlockId}."), StatusCodes.Status400BadRequest);
                break;
            case ArticleMergeError.DuplicateBlock:
                ThrowError(new ValidationFailure(BlocksErrorKey, $"The block {merged.BlockId} is in the article twice."), StatusCodes.Status400BadRequest);
                break;
            case ArticleMergeError.VisibilityNotAllowed:
                ThrowError("Only a block's owner and the DMs can change who sees it.", StatusCodes.Status403Forbidden);
                break;
        }

        var newEntryIds = await this.AppendNewEntries(
            session, req.CampaignId, member, text, "the article", req.NewEntries,
            e => NewEntryVisibility(current, merged.Blocks, e.Id), createdFromNoteId: null, ct);

        var changed = !ArticleMerge.SameBlocks(current.Article.Blocks, merged.Blocks);
        if (changed)
        {
            stream.AppendOne(new EntryArticleEdited(Actor.Member(member.MemberId), merged.Blocks));
        }
        if (changed || newEntryIds.Count > 0)
        {
            await session.SaveChangesAsync(ct);
        }
        var after = changed ? (await session.LoadAsync<Entry>(current.Id, ct))! : current;
        return (current, after, newEntryIds);
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void Conflict()
        => ThrowError(new ValidationFailure(EtagErrorKey, ConflictMessage), StatusCodes.Status409Conflict);

    /// <summary>
    /// A new entry created from an article gets the narrowest of the entry's visibility and the
    /// visibility of every block that mentions it, with the caller as its creator. The caller
    /// can see the entry and every one of those blocks, so each of those audiences contains
    /// them, and the narrowest level, relative to the caller, is inside all of them. That is
    /// also why "a DM block owned by one member and a Me block owned by another" gives
    /// <c>Me</c> for the caller.
    /// </summary>
    public static Visibility NewEntryVisibility(Entry entry, IReadOnlyList<ArticleBlock> blocks, Guid newEntryId)
    {
        var levels = blocks
            .Where(b => MentionParser.EntryIds(b.Text).Contains(newEntryId))
            .Select(b => b.Visibility)
            .Append(entry.Visibility)
            .ToList();
        return levels.Contains(Visibility.Me) ? Visibility.Me
            : levels.Contains(Visibility.DM) ? Visibility.DM
            : Visibility.Everyone;
    }
}
