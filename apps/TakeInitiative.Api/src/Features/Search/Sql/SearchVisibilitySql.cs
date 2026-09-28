namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// The read rules as SQL (invariant 5). Every fragment is the twin of the C# rule beside it,
/// and the two are checked against each other for every case by
/// <c>SearchVisibilityParityTests</c>. Getting one of these subtly wrong is the failure this
/// step exists to prevent, so they are written out in full rather than composed from pieces.
/// <para>
/// Every fragment reads the source document's own fields. Nothing copies an audience, so a
/// hide, a visibility change, a block edit or a merge is followed for free (Notes, "Why no
/// stored search table").
/// </para>
/// <para>
/// Each fragment goes into the same <c>WHERE</c> as the match, so <c>ts_rank_cd</c> and
/// <c>ts_headline</c> only ever run on rows that passed it. The providers then re-run the C#
/// rule on each document they load: a row that fails there is dropped and logged, because it
/// means SQL and C# have drifted.
/// </para>
/// <para>
/// The parameters are <c>@me</c> (the viewer's member id as text, the form Marten stores a Guid
/// in) and <c>@isDm</c>. The table alias is <c>d</c>, and a block is <c>b</c>, one element of
/// <c>jsonb_array_elements(d.data -&gt; 'Article' -&gt; 'Blocks')</c>.
/// </para>
/// </summary>
public static class SearchVisibilitySql
{
    /// <summary>
    /// <see cref="SessionNoteVisibility.VisibleTo"/>: the author whatever its visibility, a DM for
    /// <c>Everyone</c> and <c>DM</c> (hidden ones included), a player for <c>Everyone</c> and not
    /// hidden. Hiding applies to players only. A <c>Me</c> note is its author's alone.
    /// </summary>
    public static string Notes(Member viewer) => viewer.Role == Role.DM
        ? "(d.data ->> 'AuthorMemberId' = @me OR d.data ->> 'Visibility' IN ('Everyone', 'DM'))"
        : """
          (d.data ->> 'AuthorMemberId' = @me OR (d.data ->> 'Visibility' = 'Everyone'
              AND NOT coalesce((d.data ->> 'IsHidden')::boolean, false)))
          """;

    /// <summary>
    /// <see cref="EntryVisibility.VisibleTo"/>: the note table without hiding, with the creator in
    /// the author's place.
    /// </summary>
    public static string Entries(Member viewer) => viewer.Role == Role.DM
        ? "(d.data ->> 'CreatorMemberId' = @me OR d.data ->> 'Visibility' IN ('Everyone', 'DM'))"
        : "(d.data ->> 'CreatorMemberId' = @me OR d.data ->> 'Visibility' = 'Everyone')";

    /// <summary>
    /// <see cref="EntryVisibility.Listed"/>: the entries a viewer can see, as every list asks for
    /// them. A merged entry (15g) is left out everywhere: its id redirects, and its names are its
    /// target's aliases, so it is found as its target.
    /// </summary>
    public static string ListedEntries(Member viewer)
        => $"({Entries(viewer)} AND d.data ->> 'MergedIntoId' IS NULL)";

    /// <summary>
    /// The block half of <see cref="EntryVisibility.CanSeeBlock"/>:
    /// <c>Audience.Of(block.Visibility, block.OwnerMemberId).Contains(viewer)</c> — the owner
    /// always, then <c>Everyone</c> for everyone and <c>DM</c> for a DM. It is used <b>inside</b>
    /// <see cref="ListedEntries"/>, never alone, because the entry's own audience applies too.
    /// </summary>
    public const string Block = """
        (b ->> 'OwnerMemberId' = @me OR b ->> 'Visibility' = 'Everyone'
            OR (@isDm AND b ->> 'Visibility' = 'DM'))
        """;

    /// <summary>
    /// <see cref="CombatView.CanSee(Combat, Member)"/>: a DM sees every combat of the
    /// campaign, Drafts included; a player one that has started (<c>StartedAt</c> set), so a
    /// Draft, and a Draft discarded without starting, never match for them (18a.4).
    /// </summary>
    public static string Combats(Member viewer) => viewer.Role == Role.DM
        ? "(true)"
        : "(d.data ->> 'StartedAt' IS NOT NULL)";

    /// <summary>
    /// <see cref="CombatView.CanSee(Combatant, Member)"/>: a DM sees every combatant; a
    /// player one that is not hidden, or their own. A combatant is <c>c</c>, one element of
    /// <c>jsonb_array_elements(d.data -&gt; 'Combatants')</c>. It is used <b>inside</b>
    /// <see cref="Combats(Member)"/>, never alone, because the combat's own rule applies too.
    /// </summary>
    public static string Combatants(Member viewer) => viewer.Role == Role.DM
        ? "(true)"
        : "(NOT coalesce((c ->> 'Hidden')::boolean, false) OR c ->> 'OwnerMemberId' = @me)";

    /// <summary>The viewer's member id as Marten stores a Guid in JSON: the 36-character lower-case form.</summary>
    public static string Me(Member viewer) => viewer.MemberId.ToString();
}
