namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// What a parse needs. The CLI (26c) binds its flags onto this; nothing here reads configuration
/// itself, so a test can build an index with no host and no settings file.
/// </summary>
public sealed record FiveEToolsParserOptions
{
    /// <summary>Where a row's link points, unless <see cref="BaseUrl" /> says otherwise.</summary>
    public const string DefaultBaseUrl = "https://5e.tools";

    /// <summary>
    /// The count below which a folder is assumed to be the wrong one. 5eTools' bestiary is
    /// thousands of monsters; a handful means the operator pointed at a partial download, an
    /// unrelated folder, or one file.
    /// </summary>
    public const int DefaultMinMonsters = 1000;

    /// <summary>
    /// A local copy of the 5eTools source data: either a checkout, or its <c>data/</c> folder.
    /// The operator supplies it, or <see cref="FiveEToolsDownload" /> fetches a release of a
    /// repository the operator names into a folder of its own. A parse itself never fetches
    /// anything, and never reaches further than this folder.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// Write no <see cref="KnowledgeBaseItemStats" /> at all, on any row. The index then carries
    /// only identifiers.
    /// </summary>
    public bool NoStats { get; init; }

    /// <summary>The host a row's link points at. Trailing slashes are trimmed.</summary>
    public string BaseUrl { get; init; } = DefaultBaseUrl;

    /// <summary>
    /// The guard against a wrong <see cref="From" />: a build with fewer monsters than this fails
    /// rather than producing an index that a later prune would read as "everything is gone".
    /// </summary>
    public int MinMonsters { get; init; } = DefaultMinMonsters;

    /// <summary>
    /// Called with the path of each data file as it is read, so the CLI can print a progress line
    /// per file (26c). A 5eTools checkout is a few hundred files and the parse is otherwise silent
    /// for a minute; without this an operator cannot tell a slow read from a hang.
    /// </summary>
    /// <remarks>
    /// It is the only thing in a parse that observes the outside world, and it deliberately cannot
    /// influence it: the parse ignores what it returns, and a build's output does not depend on
    /// whether it is set.
    /// </remarks>
    public Action<string>? OnFileRead { get; init; }
}
