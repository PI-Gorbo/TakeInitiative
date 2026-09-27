using Microsoft.Extensions.Logging;

namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// A row came back from the search SQL that the C# read rule says the viewer cannot see. It means
/// a <see cref="SearchVisibilitySql"/> fragment and the rule it is the twin of have drifted, which
/// is a bug in the code and not a state the data can get into.
/// </summary>
public sealed class SearchVisibilityDriftException(string message) : Exception(message);

/// <summary>
/// The belt-and-braces re-check of 17a.6: the providers re-run the C# read rule on every document
/// they load, because the SQL fragment already filtered the row.
/// <para>
/// <b>A failure here fails the request.</b> The step file says such a row is "dropped and logged",
/// and dropping it is worse than what it protects against: Postgres has already applied
/// <c>LIMIT take + 1</c>, so dropping a row silently truncates a correct answer and corrupts
/// <c>hasMore</c> — a leaked row would degrade into a <i>lost visible hit</i>, which is the very
/// failure this layer exists to prevent. There is no safe way to carry on, because the rows that
/// would have filled the gap were never fetched. So the drift is logged with the row and the viewer
/// and then thrown: the request fails loudly (a 500), in a test and in production alike, instead of
/// answering with something wrong.
/// </para>
/// <para>
/// Nothing reaches this in normal operation. <c>SearchVisibilityParityTests</c> is what keeps it
/// unreachable: it checks every fragment against its C# rule for every case, so a fragment that
/// drifted fails there first. <c>SearchTests.SearchesInNormalOperation_LogNoErrors</c> pins the
/// other half — that the guard is silent while it should be.
/// </para>
/// </summary>
public static class SearchDrift
{
    /// <summary>
    /// Passes when the C# rule agrees with the SQL fragment, and otherwise logs the drift and throws.
    /// </summary>
    /// <param name="visible">What the C# read rule says about the row the SQL returned.</param>
    /// <param name="logger">The provider's logger, so the error names which provider saw it.</param>
    /// <param name="unit">The kind of row, as the message reads it: "note", "entry" or "block".</param>
    /// <param name="id">The row's id.</param>
    /// <param name="viewerId">The viewer the search ran for.</param>
    /// <param name="rule">The C# rule that disagreed, so the fix has somewhere to start.</param>
    public static void Guard(bool visible, ILogger logger, string unit, Guid id, Guid viewerId, string rule)
    {
        if (visible)
        {
            return;
        }

        logger.LogError(
            "Search returned {Unit} {Id} that member {MemberId} cannot see: the SQL visibility fragment and {Rule} have drifted.",
            unit, id, viewerId, rule);
        throw new SearchVisibilityDriftException(
            $"Search returned {unit} {id} that member {viewerId} cannot see: the SQL visibility fragment and {rule} have drifted.");
    }
}
