using FastEndpoints;
using FluentValidation;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// A filter on the session stream (glossary: Filter). Applied on the server so paging
/// stays correct. <c>Text</c> is the notes with no images and <c>Images</c> the notes with
/// some (step 16b); <c>Combats</c> matches nothing until step 18.
/// </summary>
public enum SessionStreamFilter
{
    All,
    Text,
    Images,
    Recaps,
    Combats,
    Mine,
}

public record GetSessionStreamRequest
{
    public Guid CampaignId { get; init; }
    public SessionStreamFilter? Filter { get; init; }
    /// <summary>Only sessions numbered below this. Omit it for the newest page, which ends at the current session.</summary>
    public int? Before { get; init; }
    /// <summary>How many sessions to return: 3 by default, at most 10.</summary>
    public int? Take { get; init; }
}

public class GetSessionStreamRequestValidator : Validator<GetSessionStreamRequest>
{
    public GetSessionStreamRequestValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, GetSessionStream.MaxTake);
        RuleFor(x => x.Filter).IsInEnum();
    }
}

public record SessionStreamResponse
{
    /// <summary>The page's sessions, oldest first, each with the notes the caller can see that match the filter.</summary>
    public required SessionStreamSession[] Sessions { get; init; }
    public required Guid CurrentSessionId { get; init; }
    public required bool SuggestNextSession { get; init; }
    /// <summary>Whether there are sessions older than this page.</summary>
    public required bool HasOlder { get; init; }
}

public record SessionStreamSession
{
    public required SessionResponse Session { get; init; }
    /// <summary>Ordered by <c>postedAt</c>, so a note added later sits at the end of its session.</summary>
    public required SessionNoteResponse[] Notes { get; init; }
}

/// <summary>
/// A page of the session stream, paged by session. Assembled per request from the
/// Session and SessionNote projections with the visibility rule in the SQL. Sessions
/// come back even when no note matches; the web decides whether to draw their dividers.
/// </summary>
public class GetSessionStream(IDocumentSession session, [FromKeyedServices(SessionGap.ClockKey)] TimeProvider clock)
    : Endpoint<GetSessionStreamRequest, SessionStreamResponse>
{
    public const int DefaultTake = 3;
    public const int MaxTake = 10;

    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/stream");
    }

    public override async Task HandleAsync(GetSessionStreamRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var current = await this.RequireCurrentSession(session, req.CampaignId, ct);
        var take = req.Take ?? DefaultTake;
        var before = req.Before ?? int.MaxValue;

        var newestFirst = await session.Query<Session>()
            .Where(s => s.CampaignId == req.CampaignId && s.Number < before)
            .OrderByDescending(s => s.Number)
            .Take(take + 1)
            .ToListAsync(ct);
        var page = newestFirst.Take(take).Reverse().ToList();
        var sessionIds = page.Select(s => s.Id).ToArray();
        var filter = req.Filter ?? SessionStreamFilter.All;

        // Nothing matches Combats until step 18.
        var notes = sessionIds.Length == 0 || filter is SessionStreamFilter.Combats
            ? []
            : await ApplyFilter(
                    session.Query<SessionNote>()
                        .Where(n => n.CampaignId == req.CampaignId && n.SessionId.IsOneOf(sessionIds))
                        .Where(SessionNoteVisibility.VisibleTo(member)),
                    filter,
                    member)
                .OrderBy(n => n.PostedAt)
                .ToListAsync(ct);
        var notesBySession = notes.ToLookup(n => n.SessionId);

        await SendAsync(new SessionStreamResponse
        {
            Sessions = page
                .Select(s => new SessionStreamSession
                {
                    Session = SessionResponse.From(s, current.Id),
                    Notes = notesBySession[s.Id].Select(SessionNoteResponse.From).ToArray(),
                })
                .ToArray(),
            CurrentSessionId = current.Id,
            SuggestNextSession = await SessionGap.SuggestNextSession(session, current, member, clock, ct),
            HasOlder = newestFirst.Count > take,
        }, cancellation: ct);
    }

    private static IQueryable<SessionNote> ApplyFilter(IQueryable<SessionNote> notes, SessionStreamFilter filter, Member caller)
    {
        var callerId = caller.MemberId;
        return filter switch
        {
            SessionStreamFilter.Recaps => notes.Where(n => n.IsRecap),
            SessionStreamFilter.Mine => notes.Where(n => n.AuthorMemberId == callerId),
            SessionStreamFilter.Text => notes.Where(SessionNote.WithoutImages),
            SessionStreamFilter.Images => notes.Where(SessionNote.WithImages),
            _ => notes,
        };
    }
}
