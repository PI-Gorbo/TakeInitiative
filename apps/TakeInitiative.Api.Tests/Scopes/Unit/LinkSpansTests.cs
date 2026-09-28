using FluentAssertions;
using TakeInitiative.Api.Features.LooseEnds;
using TakeInitiative.Api.Features.Search;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// Link suggestions' pure half (19b.2): which spans of a note are asked about, their offsets,
/// which matcher answers are good enough to offer, and how a note's suggestions are picked.
/// </summary>
public class LinkSpansTests
{
    private static string[] Texts(string text) => LinkSpans.From(text).Select(s => s.Text).ToArray();

    [Fact]
    public void CapitalisedWords_AndLongWords_AreSpans_ShortAndCommonOnesAreNot()
    {
        Texts("we met gundren on the road").Should().Equal("gundren", "road");
        Texts("Then we saw Al at the inn.").Should().Equal("Al");
        Texts("Halia's map, the Redbrands' hideout").Should().Equal("Halia", "Redbrands", "hideout");
    }

    [Fact]
    public void NeighbouringWords_MakeRunsOfUpToThree()
    {
        Texts("Met Sildar Hallwinter Junior Esquire today").Should().Equal(
            "Met", "Met Sildar", "Met Sildar Hallwinter",
            "Sildar", "Sildar Hallwinter", "Sildar Hallwinter Junior",
            "Hallwinter", "Hallwinter Junior", "Hallwinter Junior Esquire",
            "Junior", "Junior Esquire",
            "Esquire");
    }

    [Fact]
    public void Punctuation_AndMarkdown_BreakARun()
    {
        Texts("Gundren, Tharden").Should().Equal("Gundren", "Tharden");
        Texts("**Gundren** Rockseeker").Should().Equal("Gundren", "Rockseeker");
        Texts("Gundren\nTharden").Should().Equal("Gundren", "Tharden");
    }

    [Fact]
    public void Offsets_PointAtTheSpan_AroundMentionsAndMarkdown()
    {
        var id = Guid.NewGuid();
        var text = $"- @[Klarg](entry:{id}) and **Gundren** ran to [the Mine](https://example.com/Wave) `Code` https://x.io/Echo";
        var spans = LinkSpans.From(text);

        spans.Select(s => s.Text).Should().Equal("Gundren", "Mine");
        spans.Should().OnlyContain(s => text.Substring(s.Start, s.Length) == s.Text);
        spans[0].Start.Should().Be(text.IndexOf("Gundren", StringComparison.Ordinal));
    }

    [Fact]
    public void AMention_IsNeverASpan_NorPartOfOne()
    {
        var text = $"Gundren @[Tharden Rockseeker](entry:{Guid.NewGuid()}) Nundro";
        Texts(text).Should().Equal("Gundren", "Nundro");
    }

    [Fact]
    public void TheStopList_SkipsCommonWords_AtTheStartOfASentence()
    {
        Texts("The goblins fled. We followed. Then Klarg roared.").Should().Equal("goblins", "goblins fled", "fled", "followed", "Klarg", "Klarg roared", "roared")
            .And.NotContain(["The", "We", "Then"]);
    }

    [Fact]
    public void Unicode_Offsets_AreUtf16()
    {
        var text = "🐉 Ærwyn and Zoë";
        var spans = LinkSpans.From(text);
        spans.Select(s => s.Text).Should().Equal("Ærwyn", "Zoë");
        spans.Should().OnlyContain(s => text.Substring(s.Start, s.Length) == s.Text);
    }

    [Fact]
    public void ANote_GivesAtMostFortySpans()
    {
        var text = string.Join(". ", Enumerable.Range(0, 100).Select(i => $"Name{i}"));
        LinkSpans.From(text).Should().HaveCount(LinkSpans.MaxPerNote);
        LinkSpans.From("").Should().BeEmpty();
    }

    private static EntryMatch Match(string name, int category, double similarity = 0.9) => new(Guid.NewGuid(), name, false, category, similarity);

    [Theory]
    [InlineData("gundren", "Gundren", 0, true)]
    [InlineData("gundren", "Gundren Rockseeker", 1, true)]
    [InlineData("rockseeker", "Gundren Rockseeker", 2, true)]
    [InlineData("gund", "Gundren Rockseeker", 1, false)]
    [InlineData("seeker", "Gundren Rockseeker", 3, false)]
    [InlineData("gundran", "Gundren", 4, true)]
    public void Accepts_ExactFuzzyAndWholeWords_NeverAPartOfAWord(string span, string name, int category, bool accepted)
        => LinkSpans.Accepts(span, Match(name, category)).Should().Be(accepted);

    [Fact]
    public void Pick_KeepsTheLongestOverlap_OneEntryOnce_AtMostThree()
    {
        LinkMatch M(int start, int length, Guid entry, double similarity = 1) => new(new LinkSpan(start, length, new string('x', length)), entry, similarity);
        Guid gundren = Guid.NewGuid(), tharden = Guid.NewGuid(), a = Guid.NewGuid(), b = Guid.NewGuid();

        var picked = LinkSpans.Pick([
            M(0, 7, gundren),          // "Gundren", inside the longer span below
            M(0, 18, gundren),         // "Gundren Rockseeker"
            M(8, 10, tharden, 0.99),   // "Rockseeker" alone, overlapping: dropped
            M(30, 7, gundren),         // Gundren again, later: once per note
            M(40, 7, tharden, 0.7),
            M(40, 7, a, 0.8),          // the same span, more similar: wins
            M(50, 5, b),
            M(60, 5, Guid.NewGuid()),  // a fourth: over the cap
        ]);

        picked.Select(p => (p.Span.Start, p.Span.Length, p.EntryId)).Should().Equal(
            (0, 18, gundren), (40, 7, a), (50, 5, b));
    }
}
