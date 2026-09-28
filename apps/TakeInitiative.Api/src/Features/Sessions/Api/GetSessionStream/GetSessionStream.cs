using FastEndpoints;
using FluentValidation;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// A filter on the session stream (glossary: Filter). Applied on the server so paging
/// stays correct. <c>Text</c> is the notes with no images and <c>Images</c> the notes with
/// some (step 16b). <c>Combats</c> is the combat cards and no notes (18e); <c>All</c> is both,
/// and the others are notes only.
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
    /// <summary>The current session, or null when the campaign has none yet.</summary>
    public required Guid? CurrentSessionId { get; init; }
    public required bool SuggestNextSession { get; init; }
    /// <summary>Whether there are sessions older than this page.</summary>
    public required bool HasOlder { get; init; }
}

public record SessionStreamSession
{
    public required SessionResponse Session { get; init; }
    /// <summary>Ordered by <c>postedAt</c>, so a note added later sits at the end of its session.</summary>
    public required SessionNoteResponse[] Notes { get; init; }
    /// <summary>
    /// The combats in this session the caller can see, as cards, ordered by <c>startedAt ??
    /// createdAt</c> (18e). A DM sees their Drafts' cards; a player only started combats. Only
    /// the <c>All</c> and <c>Combats</c> filters return any.
    /// </summary>
    public required CombatCard[] Combats { get; init; }
}

/// <summary>
/// A page of the session stream, paged by session. Assembled per request from the
/// Session and SessionNote projections with the visibility rule in the SQL. Sessions
/// come back even when no note matches; the web decides whether to draw their dividers.
/// A campaign with no sessions is an empty page, not a 404.
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
        var current = await session.CurrentSession(req.CampaignId, ct);
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

        var combats = sessionIds.Length == 0 || filter is not (SessionStreamFilter.All or SessionStreamFilter.Combats)
            ? []
            : await session.Query<Combat>()
                .Where(c => c.CampaignId == req.CampaignId && c.SessionId.IsOneOf(sessionIds))
                .ToListAsync(ct);
        var cardsBySession = (await CombatCard.For(session, combats, member, ct))
            .OrderBy(c => c.StartedAt ?? c.CreatedAt)
            .ToLookup(c => c.SessionId);

        await SendAsync(new SessionStreamResponse
        {
            Sessions = page
                .Select(s => new SessionStreamSession
                {
                    Session = SessionResponse.From(s, current?.Id),
                    Notes = notesBySession[s.Id].Select(SessionNoteResponse.From).ToArray(),
                    Combats = cardsBySession[s.Id].ToArray(),
                })
                .ToArray(),
            CurrentSessionId = current?.Id,
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
