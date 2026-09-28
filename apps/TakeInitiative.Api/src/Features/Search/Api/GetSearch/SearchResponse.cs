using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// One heading of ⌘K results (glossary: Search section). Step 18 added <c>Combats</c>;
/// steps 20 and 21 add <c>Reference</c> without changing the others.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SearchSectionKey>))]
public enum SearchSectionKey
{
    Entries,
    Notes,
    Images,
    Sessions,
    Combats,
}

/// <summary>Which of a hit's payloads is filled. Step 20's REFERENCE adds a case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SearchHitKind>))]
public enum SearchHitKind
{
    Entry,
    Note,
    Session,
    Combat,
}

/// <summary>What matched on an entry: its name, one of its aliases, or a block of its article.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SearchMatchedOn>))]
public enum SearchMatchedOn
{
    Name,
    Alias,
    Article,
}

/// <summary>
/// The sections a search answers with, in the order Entries, Notes, Images, Sessions, Combats. A section
/// with no hits is left out, and the query string is never stored.
/// </summary>
public record SearchResponse
{
    /// <summary>The query as it was searched: trimmed, with the <c>@</c> prefix taken off.</summary>
    public required string Query { get; init; }
    public required SearchSection[] Sections { get; init; }
}

/// <summary>
/// One section's hits. There is no total, only <see cref="HasMore"/>: a count over rows the
/// viewer cannot see would reveal that they exist, and a count over the ones they can see would
/// cost a second pass for nothing.
/// </summary>
public record SearchSection
{
    public required SearchSectionKey Key { get; init; }
    /// <summary>Whether the section had more visible hits than <c>take</c>.</summary>
    public required bool HasMore { get; init; }
    public required SearchHit[] Hits { get; init; }
}

/// <summary>One result row. Exactly one of the payloads is set, the one <see cref="Kind"/> names.</summary>
public record SearchHit
{
    public required SearchHitKind Kind { get; init; }
    public SearchEntryHit? Entry { get; init; }
    public SearchNoteHit? Note { get; init; }
    public SearchSessionHit? Session { get; init; }
    public SearchCombatHit? Combat { get; init; }
}

/// <summary>
/// An entry hit. <see cref="MentionCount"/> is the viewer's own count, the same one
/// <c>GET entries</c> gives them. <see cref="Snippet"/> and <see cref="BlockId"/> are set only
/// for an article hit, and come from the best-ranked block the viewer can see.
/// </summary>
public record SearchEntryHit
{
    public required EntrySummaryResponse Entry { get; init; }
    public required int MentionCount { get; init; }
    public required SearchMatchedOn MatchedOn { get; init; }
    /// <summary>The alias that matched, for "aka Rockseeker". Set only when <see cref="MatchedOn"/> is <c>Alias</c>.</summary>
    public string? Alias { get; init; }
    public Guid? BlockId { get; init; }
    public Snippet? Snippet { get; init; }
}

/// <summary>
/// What a note result row needs, not the whole note: the text is in the snippet, and the viewer
/// can open the note itself either way.
/// </summary>
public record SearchNoteHit
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required int SessionNumber { get; init; }
    public required Guid AuthorMemberId { get; init; }
    public required DateTimeOffset PostedAt { get; init; }
    public required Visibility Visibility { get; init; }
    public required bool IsRecap { get; init; }
    /// <summary>The first four images, for the Images section's tiles. Empty in Notes.</summary>
    public required NoteImageResponse[] Images { get; init; }
    public required Snippet Snippet { get; init; }
}

/// <summary>A session hit. The snippet is the title's, and is absent when only the number matched.</summary>
public record SearchSessionHit
{
    public required SessionResponse Session { get; init; }
    public Snippet? Snippet { get; init; }
}

/// <summary>
/// A combat hit (18f): the viewer's own card of it, the number of the session it is in, and the
/// combatant name that matched when it was not the combat's own name. The card is built from the
/// viewer's redacted view, so it holds nothing the combat page would not show them.
/// </summary>
public record SearchCombatHit
{
    public required CombatCard Combat { get; init; }
    public required int SessionNumber { get; init; }
    /// <summary>The name of the combatant that matched, as the viewer sees it. Null when the combat's name matched.</summary>
    public string? MatchedCombatant { get; init; }
}
