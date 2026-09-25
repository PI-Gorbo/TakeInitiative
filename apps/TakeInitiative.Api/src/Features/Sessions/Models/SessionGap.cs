using Marten;

namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// The gap rule (glossary: Gap prompt). A session note more than <see cref="Gap"/> after
/// the one before it belongs to a new session. The composer uses it to suggest the next
/// session, and the Discord import (step 24) to group messages into sessions.
/// </summary>
public static class SessionGap
{
    public static readonly TimeSpan Gap = TimeSpan.FromDays(3);

    /// <summary>The DI key of the <see cref="TimeProvider"/> the gap prompt reads.</summary>
    public const string ClockKey = "SessionGap";

    public static bool IsGap(DateTimeOffset previous, DateTimeOffset next) => next - previous > Gap;

    /// <summary>
    /// True when the current session has at least one note the viewer can see, and the
    /// newest note the viewer can see in the campaign is more than <see cref="Gap"/> old.
    /// An empty current session never suggests another: it is waiting to be used. Only
    /// visible notes count, so the answer leaks nothing.
    /// </summary>
    public static async Task<bool> SuggestNextSession(
        IQuerySession session, Session current, Member viewer, TimeProvider clock, CancellationToken ct)
    {
        var visible = SessionNoteVisibility.VisibleTo(viewer);

        var currentHasNote = await session.Query<SessionNote>()
            .Where(n => n.SessionId == current.Id)
            .Where(visible)
            .AnyAsync(ct);
        if (!currentHasNote)
        {
            return false;
        }

        var newest = await session.Query<SessionNote>()
            .Where(n => n.CampaignId == current.CampaignId)
            .Where(visible)
            .OrderByDescending(n => n.PostedAt)
            .FirstOrDefaultAsync(ct);

        return newest is not null && IsGap(newest.PostedAt, clock.GetUtcNow());
    }
}
