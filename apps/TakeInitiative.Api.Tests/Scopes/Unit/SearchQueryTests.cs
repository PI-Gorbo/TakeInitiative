using FluentAssertions;
using TakeInitiative.Api.Features.Search;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// <see cref="SearchQuery.Parse"/> (17a.4): prefixes, tokens, the tsquery it builds and session
/// numbers. This is where "no search operators" is decided: the tokens are letters and digits
/// only, so nothing a member types can reach <c>to_tsquery</c> as syntax.
/// </summary>
public class SearchQueryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@")]
    [InlineData("@  ")]
    public void Parse_IsNull_ForAnEmptyQuery(string? raw)
        => SearchQuery.Parse(raw).Should().BeNull();

    [Fact]
    public void Parse_IsNull_ForAnOverlongQuery()
    {
        SearchQuery.Parse(new string('a', SearchQuery.MaxLength)).Should().NotBeNull();
        SearchQuery.Parse(new string('a', SearchQuery.MaxLength + 1)).Should().BeNull();
        // The prefix does not count towards the length.
        SearchQuery.Parse("@" + new string('a', SearchQuery.MaxLength)).Should().NotBeNull();
    }

    [Fact]
    public void Parse_TrimsAndKeepsTheText()
    {
        var query = SearchQuery.Parse("  Gundren Rockseeker  ")!;
        query.Text.Should().Be("Gundren Rockseeker");
        query.Scope.Should().Be(SearchScope.All);
    }

    [Fact]
    public void Parse_ReadsTheEntriesPrefix()
    {
        var query = SearchQuery.Parse("@ gund ")!;
        query.Scope.Should().Be(SearchScope.Entries);
        query.Text.Should().Be("gund");
    }

    [Fact]
    public void Parse_LeavesAGreaterThanPrefixAsText()
    {
        // `>` is client side (17c) and never reaches the server, so it is ordinary text here.
        var query = SearchQuery.Parse(">start")!;
        query.Scope.Should().Be(SearchScope.All);
        query.Text.Should().Be(">start");
    }

    [Theory]
    [InlineData("gund", new[] { "gund" })]
    [InlineData("Gundren Rockseeker", new[] { "gundren", "rockseeker" })]
    [InlineData("a & b | !c", new[] { "a", "b", "c" })]
    [InlineData("O'Malley's", new[] { "o", "malley", "s" })]
    [InlineData("session-12", new[] { "session", "12" })]
    public void Parse_SplitsOnAnythingThatIsNotALetterOrADigit(string raw, string[] expected)
        => SearchQuery.Parse(raw)!.Tokens.Should().Equal(expected);

    [Theory]
    [InlineData("'")]
    [InlineData(@"\")]
    [InlineData(":*")]
    [InlineData("&|!")]
    public void Parse_HasNoTokensAndNoTsQuery_ForTextWithNoLettersOrDigits(string raw)
    {
        var query = SearchQuery.Parse(raw)!;
        query.Tokens.Should().BeEmpty();
        query.TsQuery.Should().BeNull("an empty tsquery matches nothing, so there is nothing to ask Postgres");
    }

    [Fact]
    public void Parse_KeepsTheFirstEightTokens()
    {
        var query = SearchQuery.Parse("one two three four five six seven eight nine ten")!;
        query.Tokens.Should().HaveCount(SearchQuery.MaxTokens);
        query.Tokens.Last().Should().Be("eight");
    }

    [Theory]
    [InlineData("gund", "'gund':*")]
    [InlineData("Gundren Rockseeker", "'gundren' & 'rockseeker':*")]
    [InlineData("a & b | !c", "'a' & 'b' & 'c':*")]
    public void Parse_BuildsAnAndedTsQueryWithTheLastTokenAsAPrefix(string raw, string expected)
        => SearchQuery.Parse(raw)!.TsQuery.Should().Be(expected);

    [Theory]
    [InlineData("12", 12)]
    [InlineData("s12", 12)]
    [InlineData("S 12", 12)]
    [InlineData("session 12", 12)]
    [InlineData("Session 1", 1)]
    [InlineData("gundren", null)]
    [InlineData("12a", null)]
    [InlineData("s", null)]
    [InlineData("9999999999", null)]
    public void Parse_ReadsASessionNumber(string raw, int? expected)
        => SearchQuery.Parse(raw)!.SessionNumber.Should().Be(expected);

    [Theory]
    [InlineData("a", true)]
    [InlineData("5", true)]
    [InlineData("ab", false)]
    public void SingleCharacter_IsTheLengthRule(string raw, bool expected)
        => SearchQuery.Parse(raw)!.SingleCharacter.Should().Be(expected);
}
