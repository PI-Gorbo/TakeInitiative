namespace TakeInitiative.KnowledgeBase.Cli;

/// <summary>
/// What the process returns. Three values, because an operator scripting an ingest has to be able to
/// tell "the source was bad" from "the source was fine and I stopped you".
/// </summary>
public static class ExitCode
{
    /// <summary>Done, or there was nothing to do.</summary>
    public const int Ok = 0;

    /// <summary>
    /// The source could not be parsed, or there was nowhere to write it. Nothing was written —
    /// <see cref="TakeInitiative.KnowledgeBase.FiveETools.FiveEToolsParser.Build" /> returns an index
    /// or it throws, so there is no partial parse to have written half of.
    /// </summary>
    public const int ParseFailure = 1;

    /// <summary>
    /// A prune would have removed more than <c>KnowledgeBaseStore.PruneThreshold</c> of the
    /// provider's rows and <c>--force</c> was not given, so the whole run was rolled back. This is
    /// its own code because it is the one failure that means "look at what you pointed me at", not
    /// "fix the data".
    /// </summary>
    public const int PruneRefused = 2;
}
