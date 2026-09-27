namespace TakeInitiative.Api.Features.Sessions;

/// <summary>A session as every member sees it. Carries no per-viewer fields.</summary>
public record SessionResponse
{
    public required Guid Id { get; init; }
    public required int Number { get; init; }
    public string? Title { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required Guid StartedByMemberId { get; init; }
    /// <summary>Whether this is the campaign's latest session.</summary>
    public required bool IsCurrent { get; init; }

    public static SessionResponse From(Session session, Guid currentSessionId) => new()
    {
        Id = session.Id,
        Number = session.Number,
        Title = session.Title,
        StartedAt = session.StartedAt,
        StartedByMemberId = session.StartedByMemberId,
        IsCurrent = session.Id == currentSessionId,
    };
}
