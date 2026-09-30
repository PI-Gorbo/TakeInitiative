namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// A parsed 5eTools index: the rows, the source books they came from, and enough of a header to
/// tell one build from another.
/// </summary>
/// <remarks>
/// There is deliberately no timestamp anywhere in here. A rebuild from the same 5eTools data has to
/// serialise byte for byte the same, because 26c compares rows by a content hash to decide what an
/// ingest actually changed. A clock in the output would make every run report every row as updated
/// and turn the diff — and the safety rails built on it — into noise.
/// </remarks>
public sealed record FiveEToolsIndex
{
    /// <summary>The index format's version, bumped when the shape of a row changes.</summary>
    public required int Format { get; init; }

    /// <summary>
    /// The <c>version</c> from the 5eTools checkout's <c>package.json</c>, where the folder the
    /// operator pointed at has one. Null otherwise; it is a provenance note, not a key.
    /// </summary>
    public required string? FiveEToolsVersion { get; init; }

    /// <summary>How many rows of each category were written.</summary>
    public required FiveEToolsCounts Counts { get; init; }

    /// <summary>
    /// Every source book a row came from, mapped to its full title. A book with no entry in
    /// 5eTools' own catalogue maps to its own abbreviation.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Sources { get; init; }

    /// <summary>The rows, sorted by category, then name, then source.</summary>
    public required IReadOnlyList<KnowledgeBaseItem> Items { get; init; }
}

/// <summary>How many rows of each category an index holds.</summary>
public sealed record FiveEToolsCounts(int Monster, int Spell, int Item);

/// <summary>
/// What a build read and what it left out. The CLI (26c) prints this; nothing in it reaches the
/// index, so it may name a row it skipped.
/// </summary>
/// <param name="Read">How many raw rows of each category were read from the data folder.</param>
/// <param name="Skipped">The rows that were read and not written.</param>
/// <param name="Templates">
/// Copies carrying <c>_templates</c>. These are written, from their base plainly, with the template
/// not applied — see <see cref="Copy" />. The count is here so an operator can see how many rows
/// that concerns.
/// </param>
/// <param name="WithoutStats">
/// Monsters written with no <see cref="KnowledgeBaseItemStats" />, because their AC, hit points or
/// initiative could not all be read. Always zero when the parse ran with
/// <see cref="FiveEToolsParserOptions.NoStats" />, which asks for no stats at all.
/// </param>
public sealed record FiveEToolsBuildReport(
    FiveEToolsReadCounts Read,
    FiveEToolsSkipped Skipped,
    int Templates,
    int WithoutStats);

/// <summary>How many raw rows of each category a build read, before any of them were skipped.</summary>
public sealed record FiveEToolsReadCounts(int Monster, int Spell, int Item);

/// <summary>The rows a build read and did not write.</summary>
/// <param name="Ua">Rows from an Unearthed Arcana source.</param>
/// <param name="Srd52">Monsters flagged <c>srd52</c>, which the SRD provider already serves.</param>
/// <param name="MissingBase">Monsters whose <c>_copy</c> names a base that is not in the data.</param>
/// <param name="Broken">Monsters with no hit points and no <c>_copy</c> to inherit them from.</param>
/// <param name="NoName">Rows with no name or no source.</param>
public sealed record FiveEToolsSkipped(
    int Ua,
    int Srd52,
    IReadOnlyList<string> MissingBase,
    IReadOnlyList<string> Broken,
    int NoName);

/// <summary>An index and the report of how it was built.</summary>
public sealed record FiveEToolsBuildResult(FiveEToolsIndex Index, FiveEToolsBuildReport Report);
