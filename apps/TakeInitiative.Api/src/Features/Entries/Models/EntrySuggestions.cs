namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// One knowledge-base row an entry has been told it is not (28b): the row's key, and nothing else.
/// There is no actor and no timestamp here — the <see cref="EntryKnowledgeBaseSuggestionDismissed" />
/// event on the stream carries both, and history reads them from there, so copying them onto the
/// projection would only give two places to disagree.
/// </summary>
/// <param name="Provider">A reference provider's key: <c>5etools</c>.</param>
/// <param name="ItemId">The row's id within that provider: <c>monster_beholder_mm</c>.</param>
public sealed record EntrySuggestionDismissal(string Provider, string ItemId)
{
    /// <summary>
    /// Whether this is the row <paramref name="provider" /> and <paramref name="itemId" /> name. The
    /// provider folds, because <c>ReferenceCatalog.Get</c> does and two links spelled <c>5etools</c>
    /// and <c>5eTools</c> name one provider; the id does not, because it is the parser's and the
    /// table's primary key is case-sensitive. The same pair of rules
    /// <see cref="EntryLinkResolver" /> compares keys by.
    /// </summary>
    public bool Is(string provider, string itemId)
        => string.Equals(Provider, provider, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ItemId, itemId, StringComparison.Ordinal);
}

/// <summary>
/// Who sees a knowledge-base suggestion on an entry (28b): <b>exactly the members who could accept
/// one</b> — <see cref="EntryLinks.CanRead" /> <i>and</i> <see cref="EntryLinks.CanWrite" />. Both
/// halves matter, and for different reasons.
/// <list type="bullet">
/// <item>The <b>read</b> half stops a disclosure. "Is this the Beholder from the Monster Manual?" on
/// an unclaimed Character is the stat-block leak <see cref="EntryLinks.CanRead" /> exists to prevent
/// (invariant 8) — arguably worse, since it names the monster without anyone having linked it.</item>
/// <item>The <b>write</b> half stops a useless prompt. A question whose only available answer is
/// "dismiss" is noise.</item>
/// </list>
/// <para>
/// <b>The endpoint answers an empty list rather than a 403 when this is false</b>, and that is the
/// point of having it as its own rule. A 403 would tell a player "there is something here you may not
/// see", which is the same leak in a different form; invariant 5's "hidden things are absent, not
/// greyed out" applies to status codes. It is the mirror image of the trap
/// <see cref="EntryLinks.CanWrite" />'s remarks describe for <c>DELETE</c>: there, adding a read
/// check turns a 404 into a 403 and discloses a link; here, letting a failed read check reach the
/// authorisation filter would turn an empty answer into a 403 and disclose a suggestion.
/// </para>
/// </summary>
public static class EntrySuggestions
{
    /// <summary>How many candidates the prompt may offer at once (28b): the best three.</summary>
    public const int MaxSuggestions = 3;

    /// <summary>The longest <c>provider</c> a dismissal may name. A provider key is a word.</summary>
    public const int ProviderMaxLength = 40;

    /// <summary>The longest <c>itemId</c> a dismissal may name, which is the parser's own id.</summary>
    public const int ItemIdMaxLength = 200;

    /// <summary>See the class summary: read <b>and</b> write, and an empty answer rather than a 403.</summary>
    public static bool CanSee(Entry entry, Member viewer)
        => EntryLinks.CanRead(entry, viewer) && EntryLinks.CanWrite(entry, viewer);
}
