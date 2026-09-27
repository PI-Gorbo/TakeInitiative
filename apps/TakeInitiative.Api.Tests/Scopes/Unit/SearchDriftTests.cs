using FluentAssertions;
using Microsoft.Extensions.Logging;
using TakeInitiative.Api.Features.Search;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// <see cref="SearchDrift.Guard"/> (17a.6, "Belt and braces"): what the providers do with a row the
/// SQL returned and the C# read rule refuses.
/// <para>
/// The decision this pins is that such a row <b>fails the request</b> rather than being dropped.
/// Postgres has already applied <c>LIMIT take + 1</c> by then, so dropping a row shortens a correct
/// answer and flips <c>hasMore</c> — a leak would degrade into a lost visible hit, the very failure
/// the re-check exists to prevent — and the rows that would have filled the gap were never fetched,
/// so there is nothing to carry on with.
/// </para>
/// <para>
/// It is tested here and not through the API because drift cannot be induced from outside: both
/// sides read the same fields of the same document, so no state a request can reach makes SQL accept
/// a row the C# rule refuses. Weakening a production rule to stage one would test the staging.
/// <c>SearchVisibilityParityTests</c> is what keeps the guard unreachable, and
/// <c>SearchTests.SearchesInNormalOperation_LogNoError</c> keeps it quiet.
/// </para>
/// </summary>
public class SearchDriftTests
{
    private static readonly Guid RowId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ViewerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Guard_DoesNothing_WhenTheRuleAgrees()
    {
        var logger = new RecordingLogger();
        SearchDrift.Guard(visible: true, logger, "note", RowId, ViewerId, "SessionNoteVisibility");
        logger.Records.Should().BeEmpty("nothing happened, so nothing is logged");
    }

    [Fact]
    public void Guard_LogsAnError_AndThrows_WhenTheRuleDisagrees()
    {
        var logger = new RecordingLogger();

        var thrown = Assert.Throws<SearchVisibilityDriftException>(
            () => SearchDrift.Guard(visible: false, logger, "note", RowId, ViewerId, "SessionNoteVisibility"));

        thrown.Message.Should().Contain(RowId.ToString()).And.Contain(ViewerId.ToString())
            .And.Contain("SessionNoteVisibility").And.Contain("drifted");
        var record = logger.Records.Should().ContainSingle().Subject;
        record.Level.Should().Be(LogLevel.Error);
        record.Message.Should().Contain(RowId.ToString()).And.Contain(ViewerId.ToString());
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Records.Add((logLevel, formatter(state, exception)));
    }
}
