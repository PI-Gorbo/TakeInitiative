using FluentAssertions;
using TakeInitiative.Api.Features.Search;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// <see cref="Snippet.From"/> (17a.7): the markers <c>ts_headline</c> puts in become ranges over
/// plain text, and the markdown markers are dropped while that happens, so the offsets are right
/// for the text the reader sees. The API never sends HTML.
/// </summary>
public class SnippetTests
{
    private const string Start = "";
    private const string Stop = "";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void From_IsNull_WhenThereIsNothingToShow(string? headline)
        => Snippet.From(headline).Should().BeNull();

    [Fact]
    public void From_TurnsMarkersIntoRanges()
    {
        var snippet = Snippet.From($"We met {Start}Gundren{Stop} on the road")!;
        snippet.Text.Should().Be("We met Gundren on the road");
        snippet.Highlights.Should().ContainSingle();
        snippet.Highlights[0].Start.Should().Be(7);
        snippet.Highlights[0].Length.Should().Be(7);
        snippet.Text.Substring(snippet.Highlights[0].Start, snippet.Highlights[0].Length).Should().Be("Gundren");
    }

    [Fact]
    public void From_ReadsSeveralRanges_IncludingAtTheStartAndTheEnd()
    {
        var snippet = Snippet.From($"{Start}Gundren{Stop} met {Start}Klarg{Stop}")!;
        snippet.Text.Should().Be("Gundren met Klarg");
        snippet.Highlights.Should().HaveCount(2);
        snippet.Highlights[0].Start.Should().Be(0);
        snippet.Text.Substring(snippet.Highlights[1].Start, snippet.Highlights[1].Length).Should().Be("Klarg");
    }

    [Fact]
    public void From_DropsEmphasisAndCodeMarkers_AndKeepsTheOffsetsRight()
    {
        var snippet = Snippet.From($"a **bold** word {Start}Gundren{Stop} and `code` and _under_")!;
        snippet.Text.Should().Be("a bold word Gundren and code and under");
        snippet.Text.Substring(snippet.Highlights[0].Start, snippet.Highlights[0].Length).Should().Be("Gundren");
    }

    [Fact]
    public void From_DropsAHeadingOrQuoteMarkerAtTheStartOfALine()
    {
        Snippet.From("## A heading")!.Text.Should().Be("A heading");
        Snippet.From("> quoted")!.Text.Should().Be("quoted");
        Snippet.From("first\n> quoted")!.Text.Should().Be("first\nquoted");
    }

    [Fact]
    public void From_KeepsAGreaterThanThatIsNotALineMarker()
        => Snippet.From("a > b")!.Text.Should().Be("a > b");

    [Fact]
    public void From_DropsMarkerCharactersThatCameFromUserText()
    {
        // SearchSql.PlainText deletes them from the source, so this is the belt to that braces:
        // an unpaired marker never becomes a highlight.
        var snippet = Snippet.From($"before {Start}after")!;
        snippet.Text.Should().Be("before after");
        snippet.Highlights.Should().BeEmpty();
    }

    [Fact]
    public void From_IgnoresAnEmptyRange()
    {
        var snippet = Snippet.From($"a{Start}{Stop}b")!;
        snippet.Text.Should().Be("ab");
        snippet.Highlights.Should().BeEmpty();
    }

    [Fact]
    public void From_IsNull_WhenOnlyMarkersAreLeft()
        => Snippet.From($"{Start}{Stop}").Should().BeNull();

    // OrEmpty: a hit is never dropped for want of a highlight (17a.6, and SearchDrift).

    [Fact]
    public void OrEmpty_KeepsAHeadlineThatHasNothingToHighlight()
    {
        // What a session title matched down the trigram ladder looks like: the words matched, but
        // there is no tsquery lexeme in them to mark. The hit is real, so the words are shown.
        var snippet = Snippet.OrEmpty("Wagons of Triboar");
        snippet.Text.Should().Be("Wagons of Triboar");
        snippet.Highlights.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void OrEmpty_IsAnEmptySnippet_AndNeverNull(string? headline)
    {
        // An empty headline is not drift and must not cost the viewer the hit: the caller shows the
        // row with nothing under it rather than dropping a row Postgres already counted for hasMore.
        var snippet = Snippet.OrEmpty(headline);
        snippet.Text.Should().BeEmpty();
        snippet.Highlights.Should().BeEmpty();
    }

    // Escapes are dropped by SearchSql.PlainText, which is SQL, so the offsets after an escape is
    // removed cannot be asserted here: Snippet.From never sees a backslash that PlainText took out.
    // SearchTests.ASnippetOfEscapedText_DropsTheEscapes_AndKeepsTheOffsetsRight covers that claim
    // where it can be covered honestly, over Postgres, and the markdown markers this class does drop
    // are covered above.
}
