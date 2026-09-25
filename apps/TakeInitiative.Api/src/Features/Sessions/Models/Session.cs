using Marten;
using Marten.Events;

namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// Inline projection of a Session stream (stream id = session id). The current session
/// is the campaign's session with the highest <see cref="Number"/>; a unique index on
/// (CampaignId, Number) stops two members starting the same number twice.
/// </summary>
public record Session
{
    public Guid Id { get; init; }
    public Guid CampaignId { get; init; }
    public int Number { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public Guid StartedByMemberId { get; init; }

    public static Session Create(IEvent<SessionStarted> @event) => new()
    {
        Id = @event.StreamId,
        CampaignId = @event.Data.CampaignId,
        Number = @event.Data.Number,
        StartedAt = @event.Timestamp,
        StartedByMemberId = @event.Data.Actor.MemberId,
    };

    public Session Apply(SessionTitleChanged e) => this with { Title = e.Title };

    public const int TitleMaxLength = 100;

    /// <summary>A title is trimmed, and blank clears it.</summary>
    public static string? NormaliseTitle(string? title)
        => string.IsNullOrWhiteSpace(title) ? null : title.Trim();
}

public static class SessionQueries
{
    /// <summary>The current session: the campaign's session with the highest number, or null before Session 1.</summary>
    public static Task<Session?> CurrentSession(this IQuerySession session, Guid campaignId, CancellationToken ct)
        => session.Query<Session>()
            .Where(s => s.CampaignId == campaignId)
            .OrderByDescending(s => s.Number)
            .FirstOrDefaultAsync(ct);
}
