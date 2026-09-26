using System.Linq.Expressions;

namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// The one read rule for session notes (invariant 5). Every note query filters with
/// <see cref="VisibleTo"/>; single loads use <see cref="CanSee"/>.
/// A viewer sees a note when:
/// <list type="bullet">
/// <item>they wrote it (whatever its visibility, hidden or not);</item>
/// <item>they are a DM and it is <c>Everyone</c> or <c>DM</c> (hidden ones included, marked);</item>
/// <item>they are a Player and it is <c>Everyone</c> and not hidden.</item>
/// </list>
/// A <c>Me</c> note is its author's alone. A note the caller cannot see is a 404, never
/// a 403. The push-side twin, <see cref="SessionNoteAudience"/>, must change with this;
/// <c>SessionNoteAudienceTests</c> checks the two agree for every case.
/// </summary>
public static class SessionNoteVisibility
{
    public static Expression<Func<SessionNote, bool>> VisibleTo(Member viewer)
    {
        var viewerId = viewer.MemberId;
        return viewer.Role == Role.DM
            ? n => n.AuthorMemberId == viewerId || n.Visibility == Visibility.Everyone || n.Visibility == Visibility.DM
            : n => n.AuthorMemberId == viewerId || (n.Visibility == Visibility.Everyone && !n.IsHidden);
    }

    public static bool CanSee(SessionNote note, Member viewer) => SessionNoteAudience.Of(note).Contains(viewer);
}
