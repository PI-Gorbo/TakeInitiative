using System.Linq.Expressions;

namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// The one read rule for session notes (invariant 5). Every note query filters with
/// <see cref="VisibleTo"/>; single loads (and the hub, from 14b) use <see cref="CanSee"/>.
/// A viewer sees a note when:
/// <list type="bullet">
/// <item>they wrote it (whatever its visibility, hidden or not);</item>
/// <item>they are a DM and it is <c>Everyone</c> or <c>DM</c> (hidden ones included, marked);</item>
/// <item>they are a Player and it is <c>Everyone</c> and not hidden.</item>
/// </list>
/// A <c>Me</c> note is its author's alone. A note the caller cannot see is a 404, never
/// a 403. The push-side twin (the audience groups, 14b) must change with this.
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

    public static bool CanSee(SessionNote note, Member viewer)
    {
        if (note.AuthorMemberId == viewer.MemberId)
        {
            return true;
        }

        return viewer.Role == Role.DM
            ? note.Visibility is Visibility.Everyone or Visibility.DM
            : note.Visibility == Visibility.Everyone && !note.IsHidden;
    }
}
