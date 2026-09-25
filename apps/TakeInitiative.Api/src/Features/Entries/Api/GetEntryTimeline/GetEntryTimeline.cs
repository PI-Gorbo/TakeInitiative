using FastEndpoints;
using FluentValidation;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record GetEntryTimelineRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>Only notes posted before this (a <c>postedAt</c> from the previous page). Omit it for the newest page.</summary>
    public DateTimeOffset? Before { get; init; }
    /// <summary>How many notes to return: 20 by default, at most 50.</summary>
    public int? Take { get; init; }
}

public class GetEntryTimelineRequestValidator : Validator<GetEntryTimelineRequest>
{
    public GetEntryTimelineRequestValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, GetEntryTimeline.MaxTake);
    }
}

public record EntryTimelineResponse
{
    /// <summary>The page's notes, oldest first.</summary>
    public required EntryTimelineItem[] Items { get; init; }
    /// <summary>Whether there are older notes: ask again with <c>before</c> = the first item's <c>postedAt</c>.</summary>
    public required bool HasOlder { get; init; }
    /// <summary>
    /// The other entries whose articles mention this one, in blocks the caller can see (15e),
    /// ordered by entry name. The same on every page; blocks have no time, so they are not
    /// interleaved with the notes.
    /// </summary>
    public required EntryArticleMention[] ArticleMentions { get; init; }
}

/// <summary>An entry whose article mentions the timeline's entry, and the visible blocks that do, in article order.</summary>
public record EntryArticleMention
{
    public required Guid EntryId { get; init; }
    public required Guid[] BlockIds { get; init; }
}

public record EntryTimelineItem
{
    public required SessionNoteResponse Note { get; init; }
    public required int SessionNumber { get; init; }
}

/// <summary>
/// An entry's timeline (glossary: Timeline): the session notes the caller can see that
/// mention it, newest page first and oldest first within a page, from
/// <see cref="MentionIndex.NotesMentioning"/>, plus the articles whose visible blocks mention
/// it (<see cref="MentionIndex.BlocksMentioning"/>). It is read-only: editing a note or an
/// article is the only way to change it. An entry the caller cannot see is a 404.
/// </summary>
public class GetEntryTimeline(IDocumentSession session) : Endpoint<GetEntryTimelineRequest, EntryTimelineResponse>
{
    public const int DefaultTake = 20;
    public const int MaxTake = 50;

    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/entries/{EntryId}/timeline");
    }

    public override async Task HandleAsync(GetEntryTimelineRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);

        // A merged entry's mentions are this entry's (15g): the text keeps the old id.
        var ids = entry.MentionIds();
        var page = await MentionIndex.NotesMentioning(
            session, req.CampaignId, ids, member, req.Before, req.Take ?? DefaultTake, ct);

        var blocks = await MentionIndex.BlocksMentioning(session, req.CampaignId, ids, member, ct);

        var sessionIds = page.Notes.Select(n => n.SessionId).Distinct().ToArray();
        var numbers = sessionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : (await session.LoadManyAsync<Session>(ct, sessionIds)).ToDictionary(s => s.Id, s => s.Number);

        await SendAsync(new EntryTimelineResponse
        {
            Items = page.Notes
                .Select(n => new EntryTimelineItem { Note = SessionNoteResponse.From(n), SessionNumber = numbers[n.SessionId] })
                .ToArray(),
            HasOlder = page.HasOlder,
            ArticleMentions = blocks
                .GroupBy(b => b.Entry.Id)
                .Select(g => new EntryArticleMention { EntryId = g.Key, BlockIds = g.Select(b => b.Block.Id).ToArray() })
                .ToArray(),
        }, cancellation: ct);
    }
}
