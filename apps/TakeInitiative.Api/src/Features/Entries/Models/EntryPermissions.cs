namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// Who may change an entry (invariant 4). Both assume the caller can already see it;
/// <see cref="EntryAccess"/> checks visibility first, so a hidden entry is a 404 before
/// either of these can make it a 403.
/// </summary>
public static class EntryPermissions
{
    /// <summary>
    /// Name, kind and aliases (and from 15e the article, promote and merge): a DM, the
    /// creator, or anyone who can see it when its edit access is <c>Anyone</c>.
    /// </summary>
    public static bool CanEdit(Entry entry, Member member)
        => member.Role == Role.DM
            || entry.CreatorMemberId == member.MemberId
            || (entry.EditAccess == EditAccess.Anyone && EntryVisibility.CanSee(entry, member));

    /// <summary>Visibility and edit access: the creator and DMs.</summary>
    public static bool CanChangeAccess(Entry entry, Member member)
        => member.Role == Role.DM || entry.CreatorMemberId == member.MemberId;
}
