using System.Text.RegularExpressions;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PostEntryQuoteRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>The session note to quote. It must be one the caller can see in this campaign.</summary>
    public required Guid NoteId { get; init; }
    /// <summary>
    /// The excerpt, in the note's stored (markdown) form. Left out means the whole note (the
    /// phone's flow). Otherwise, after collapsing whitespace, it must be part of the note's text.
    /// </summary>
    public string? Text { get; init; }
}

public class PostEntryQuoteRequestValidator : Validator<PostEntryQuoteRequest>
{
    public PostEntryQuoteRequestValidator()
    {
        RuleFor(x => x.NoteId).NotEmpty();
        RuleFor(x => x.Text)
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("A quote needs some text. Leave it out to quote the whole note.")
            .MaximumLength(SessionNote.TextMaxLength)
            .When(x => x.Text is not null);
    }
}

/// <summary>The entry after the promote, as the caller sees it, and the new quote's block id.</summary>
public record EntryQuoteResponse
{
    public required EntryResponse Entry { get; init; }
    /// <summary>The quote's block. The caller can always see it: its audience is the note's, and they can see the note.</summary>
    public required Guid BlockId { get; init; }
}

/// <summary>
/// Promote (15e.5): copies a session note, or part of it, to the end of an entry's article as
/// a quote that links back to the note. Anyone who can edit the entry can promote a note they
/// can see. The quote is a real excerpt: its owner is the note's author, and its visibility is
/// the note's audience (a hidden <c>Everyone</c> note gives <c>DM</c>), so it reaches exactly
/// the people who could read the note. The promoter is the event's actor and the quote's
/// <c>promotedByMemberId</c>. Promoting the same text twice is allowed.
/// </summary>
public partial class PostEntryQuote(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PostEntryQuoteRequest, EntryQuoteResponse>
{
    public const string TextErrorKey = "text";
    public const string BlocksErrorKey = "blocks";

    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/entries/{EntryId}/quotes");
    }

    public override async Task HandleAsync(PostEntryQuoteRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        this.RequireCanEdit(entry, member);
        var note = await this.RequireVisibleNote(session, req.CampaignId, req.NoteId, member, ct);

        var text = req.Text?.Trim() ?? note.Text;
        if (req.Text is not null && !Collapse(note.Text).Contains(Collapse(text), StringComparison.Ordinal))
        {
            ThrowError(new ValidationFailure(TextErrorKey, "A quote must be text from the note."), StatusCodes.Status400BadRequest);
        }

        // The limits are checked on what the caller can see, like PUT article, so they reveal
        // nothing about hidden blocks.
        var visible = ArticleView.VisibleBlocks(entry, member);
        if (visible.Count + 1 > Article.MaxBlocks)
        {
            ThrowError(new ValidationFailure(BlocksErrorKey, $"An article can have at most {Article.MaxBlocks} blocks."), StatusCodes.Status400BadRequest);
        }
        if (visible.Sum(b => b.Text.Length) + text.Length > Article.MaxCharacters)
        {
            ThrowError(new ValidationFailure(TextErrorKey, $"An article can be at most {Article.MaxCharacters:N0} characters long."), StatusCodes.Status400BadRequest);
        }

        var sessionNumber = (await session.LoadAsync<Session>(note.SessionId, ct))!.Number;
        var block = new ArticleBlock
        {
            Id = Guid.NewGuid(),
            Text = text,
            Visibility = NewEntries.VisibilityFrom(note),
            OwnerMemberId = note.AuthorMemberId,
            Quote = new ArticleQuote
            {
                NoteId = note.Id,
                SessionId = note.SessionId,
                SessionNumber = sessionNumber,
                AuthorMemberId = note.AuthorMemberId,
                PromotedByMemberId = member.MemberId,
                PromotedAt = Microseconds.Truncate(DateTimeOffset.UtcNow),
            },
        };

        // Appended at the end without a version check: it commutes with other promotes, and
        // an article edit racing it fails its own version check and sees the quote.
        session.Events.Append(entry.Id, new EntryQuotePromoted(Actor.Member(member.MemberId), block));
        await session.SaveChangesAsync(ct);

        var after = (await session.LoadAsync<Entry>(entry.Id, ct))!;
        var before = after with { Article = new Article { Blocks = after.Article.Blocks.Where(b => b.Id != block.Id).ToList() } };
        await hub.NotifyEntryArticleChanged(campaign.Members, before, after);

        await SendAsync(new EntryQuoteResponse { Entry = EntryResponse.From(after, member), BlockId = block.Id }, cancellation: ct);
    }

    /// <summary>Runs of whitespace as one space, trimmed: a selection's line breaks need not match the source's.</summary>
    public static string Collapse(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
