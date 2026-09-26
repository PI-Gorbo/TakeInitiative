using System.Linq.Expressions;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// The one read rule for entries (invariant 5): the session-note table without hiding,
/// with the creator in the author's place. Every entry query filters with
/// <see cref="VisibleTo"/>; single loads use <see cref="CanSee"/>.
/// <list type="bullet">
/// <item><c>Everyone</c>: every member;</item>
/// <item><c>DM</c>: the creator and every DM;</item>
/// <item><c>Me</c>: the creator only.</item>
/// </list>
/// An entry the caller cannot see is a 404, never a 403, and is left out of lists and
/// counts. The push-side twin is <see cref="EntryAudience"/>; both come from
/// <see cref="Audience"/>, and <c>EntryAudienceTests</c> checks the three agree.
/// </summary>
public static class EntryVisibility
{
    public static Expression<Func<Entry, bool>> VisibleTo(Member viewer)
    {
        var viewerId = viewer.MemberId;
        return viewer.Role == Role.DM
            ? e => e.CreatorMemberId == viewerId || e.Visibility == Visibility.Everyone || e.Visibility == Visibility.DM
            : e => e.CreatorMemberId == viewerId || e.Visibility == Visibility.Everyone;
    }

    public static bool CanSee(Entry entry, Member viewer) => EntryAudience.Of(entry).Contains(viewer);

    /// <summary>
    /// Who sees one block of an article (15e): the viewer must see the entry <b>and</b> be in
    /// the block's own audience, <c>Audience.Of(block.Visibility, block.OwnerMemberId)</c>.
    /// A block that fails either is absent from every read and push, with no placeholder and
    /// no count.
    /// </summary>
    public static bool CanSeeBlock(Entry entry, ArticleBlock block, Member viewer)
        => CanSee(entry, viewer) && BlockAudience(block).Contains(viewer);

    public static Audience BlockAudience(ArticleBlock block) => Audience.Of(block.Visibility, block.OwnerMemberId);
}
