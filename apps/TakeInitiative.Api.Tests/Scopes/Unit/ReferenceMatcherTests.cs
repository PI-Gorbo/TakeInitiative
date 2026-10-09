using FluentAssertions;
using TakeInitiative.Api.Features.Reference;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// The in-memory match ladder (20a.5), the port of <c>SearchSql.MatchCategory</c> and pg_trgm's
/// <c>word_similarity</c>.
/// </summary>
public class ReferenceMatcherTests
{
    private static IReadOnlyList<(string Item, int Category, double Similarity)> Search(string text, int take, params string[] names)
        => ReferenceMatcher.Search(text, names.Select(n => new ReferenceMatcher.Candidate<string>(n, n, ReferenceMatcher.Fold(n))), take);

    [Theory]
    [InlineData("Goblin", "goblin", 0)]
    [InlineData("Goblin Warrior", "gob", 1)]
    [InlineData("Goblin Warrior", "warrior", 2)]
    [InlineData("Half-Red Dragon", "red", 2)]
    [InlineData("Hobgoblin", "goblin", 3)]
    public void EachRung(string name, string query, int category)
        => Search(query, 5, name).Should().ContainSingle().Which.Category.Should().Be(category);

    [Fact]
    public void Fuzzy_CatchesATypo()
    {
        var hit = Search("gobln", 5, "Goblin Warrior", "Owlbear").Should().ContainSingle().Subject;
        hit.Item.Should().Be("Goblin Warrior");
        hit.Category.Should().Be(4);
    }

    [Fact]
    public void Fuzzy_NeedsThreeCharacters()
        => Search("gx", 5, "Goblin", "Gx Something").Select(h => h.Item).Should().Equal("Gx Something");

    [Fact]
    public void Accents_AreFolded()
    {
        Search("gundren", 5, "Gündren Rockseeker").Should().ContainSingle().Which.Category.Should().Be(1);
        Search("GÜNDREN", 5, "Gundren").Should().ContainSingle().Which.Category.Should().Be(0);
    }

    [Fact]
    public void Ranking_IsCategoryThenSimilarityThenLengthThenName()
    {
        var hits = Search("goblin", 10, "Hobgoblin", "Goblin Warrior", "Goblin Boss", "Goblin", "Goblin Minion", "Owlbear");
        hits.Select(h => h.Item).Should().Equal("Goblin", "Goblin Boss", "Goblin Minion", "Goblin Warrior", "Hobgoblin");
        hits.Select(h => h.Category).Should().Equal(0, 1, 1, 1, 3);
    }

    [Fact]
    public void Ties_BreakByTheShorterNameThenTheName()
        => Search("bear", 10, "Brown Bear", "Black Bear", "Polar Bear").Select(h => h.Item)
            .Should().Equal("Black Bear", "Brown Bear", "Polar Bear");

    [Fact]
    public void Take_LimitsTheHits()
    {
        Search("goblin", 2, "Goblin Warrior", "Goblin Boss", "Goblin Minion").Should().HaveCount(2);
        Search("goblin", 0, "Goblin").Should().BeEmpty();
        Search("   ", 5, "Goblin").Should().BeEmpty();
    }

    [Fact]
    public void WordSimilarity_MatchesPgTrgm()
    {
        // The pg_trgm documentation's example.
        ReferenceMatcher.WordSimilarity("word", "two words").Should().BeApproximately(0.8, 1e-9);
        ReferenceMatcher.WordSimilarity("goblin", "goblin warrior").Should().Be(1);
        ReferenceMatcher.WordSimilarity("zzz", "goblin").Should().Be(0);
    }

    [Fact]
    public void Trigrams_ArePaddedPerWord()
        => ReferenceMatcher.Trigrams("cat, ox").Should().Equal("  c", " ca", "cat", "at ", "  o", " ox", "ox ");
}
