namespace TakeInitiative.Api.Features.Entries;

/// <summary>Why a merge is refused, before anything is appended.</summary>
public enum EntryMergeError
{
    None,
    /// <summary>Someone who can see the target cannot see the merged entry.</summary>
    WouldReveal,
    /// <summary>The merged entry is claimed, and the target is not a Character.</summary>
    ClaimNeedsCharacter,
    /// <summary>The merged entry is claimed, and the target has another claimer.</summary>
    ClaimedByAnother,
}

/// <summary>
/// The rules of a merge (glossary: Merge, 15g.1), pure so they can be tested on their own.
/// </summary>
public static class EntryMerge
{
    /// <summary>
    /// The visibility guard: everyone in the campaign who can see <paramref name="into"/> can
    /// already see <paramref name="from"/>, that is <c>Audience(into) ⊆ Audience(from)</c> over
    /// the current members. Otherwise the merged entry's name (which becomes an alias) and its
    /// article would reach people who could not see them. Revealing is a deliberate visibility
    /// change first ("nothing is revealed automatically", invariant 5).
    /// </summary>
    public static bool RevealsNothing(Entry from, Entry into, IEnumerable<Member> members)
        => members.All(m => !EntryVisibility.CanSee(into, m) || EntryVisibility.CanSee(from, m));

    /// <summary>The members who can see the merged entry but will not see the target: the web's "Players will no longer see …".</summary>
    public static IReadOnlyList<Member> WhoLoses(Entry from, Entry into, IEnumerable<Member> members)
        => members.Where(m => EntryVisibility.CanSee(from, m) && !EntryVisibility.CanSee(into, m)).ToList();

    public static EntryMergeError Check(Entry from, Entry into, IEnumerable<Member> members)
    {
        if (!RevealsNothing(from, into, members))
        {
            return EntryMergeError.WouldReveal;
        }
        if (from.ClaimedByMemberId is { } claimer)
        {
            if (into.Kind != EntryKind.Character)
            {
                return EntryMergeError.ClaimNeedsCharacter;
            }
            if (into.ClaimedByMemberId is { } other && other != claimer)
            {
                return EntryMergeError.ClaimedByAnother;
            }
        }
        return EntryMergeError.None;
    }

    /// <summary>
    /// The stats the target takes from the merged entry: none when it has its own, or when
    /// taking them would widen who reads them. Stats on an unclaimed entry are for the DMs only,
    /// so they move onto a target that will be claimed only if the merged entry was claimed too
    /// (its stats were then already readable by everyone who sees it, which covers the target's
    /// audience by the guard).
    /// </summary>
    public static Stats? AdoptedStats(Entry from, Entry into)
    {
        if (into.Stats is not null || from.Stats is null || from.Kind != EntryKind.Character || into.Kind != EntryKind.Character)
        {
            return null;
        }
        var claimedAfter = (into.ClaimedByMemberId ?? from.ClaimedByMemberId) is not null;
        var claimedBefore = from.ClaimedByMemberId is not null;
        return claimedAfter && !claimedBefore ? null : from.Stats;
    }

    /// <summary>The text of the ordinary block that heads the merged entry's blocks.</summary>
    public static string HeadingText(string fromName) => $"Merged from {fromName}";

    public static ArticleBlock Heading(EntryAbsorbed e) => new()
    {
        Id = e.HeadingBlockId,
        Text = HeadingText(e.FromName),
        Visibility = Visibility.Everyone,
        OwnerMemberId = e.Actor.MemberId,
    };
}
