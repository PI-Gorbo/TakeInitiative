using FluentAssertions;
using TakeInitiative.Api.Features.Connections;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// The evidence snippet (19a.4): the window around both mentions, word boundaries, mentions
/// never cut, and two windows joined by <c>…</c> when the mentions are far apart.
/// </summary>
public class EvidenceSnippetTests
{
    private static readonly Guid Gundren = Guid.NewGuid(), Tharden = Guid.NewGuid(), Merged = Guid.NewGuid();

    private static string M(string name, Guid id) => $"@[{name}](entry:{id})";

    private static string Words(int from, int count) => string.Join(" ", Enumerable.Range(from, count).Select(i => $"word{i}"));

    private static string Cut(string text) => EvidenceSnippet.Cut(text, [Gundren, Merged], [Tharden]);

    /// <summary>Every whole word of the snippet (the cut markers aside) is a word of the source.</summary>
    private static void ShouldCutOnWords(string snippet, string source)
    {
        var words = source.Split(' ').ToHashSet();
        snippet.Replace("…", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).Should().OnlyContain(w => words.Contains(w));
    }

    [Fact]
    public void AShortText_IsWhole()
    {
        var text = $"  {M("Gundren", Gundren)} met {M("Tharden", Tharden)}.  ";
        Cut(text).Should().Be(text.Trim());
    }

    [Fact]
    public void ALongText_IsAWindowAroundBothMentions_CutOnWords()
    {
        var text = $"{Words(0, 100)} {M("Gundren", Gundren)} met {M("Tharden", Tharden)} {Words(100, 100)}";
        var snippet = Cut(text);

        snippet.Should().StartWith("…").And.EndWith("…");
        snippet.Should().Contain($"{M("Gundren", Gundren)} met {M("Tharden", Tharden)}");
        snippet.Length.Should().BeInRange(EvidenceSnippet.Window - 20, EvidenceSnippet.Window + 2);
        ShouldCutOnWords(snippet, text);
    }

    [Fact]
    public void AMergedId_IsAMentionOfItsEntry_AndTheStartHasNoEllipsis()
    {
        var text = $"{M("Gob", Merged)} and {M("Tharden", Tharden)} {Words(0, 200)}";
        var snippet = Cut(text);
        snippet.Should().StartWith(M("Gob", Merged)).And.EndWith("…");
        ShouldCutOnWords(snippet, text);
    }

    [Fact]
    public void AMention_IsNeverCutInHalf()
    {
        // A long multi-word mention sits right where the window would end.
        var name = string.Join(" ", Enumerable.Range(0, 30).Select(i => $"name{i}"));
        var text = $"{M("Gundren", Gundren)} and {M("Tharden", Tharden)} {Words(0, 20)} @[{name}](entry:{Guid.NewGuid()}) {Words(20, 100)}";
        var snippet = Cut(text);
        var opened = snippet.Split("@[").Length - 1;
        var closed = snippet.Split("](entry:").Length - 1;
        opened.Should().Be(closed);
    }

    [Fact]
    public void MentionsFarApart_KeepTheFirstOfEach_WithAnEllipsisBetween()
    {
        var text = $"{Words(0, 20)} {M("Gundren", Gundren)} {Words(20, 150)} {M("Tharden", Tharden)} {Words(170, 20)} {M("Gundren", Gundren)}";
        var snippet = Cut(text);

        snippet.Should().Contain(M("Gundren", Gundren)).And.Contain(M("Tharden", Tharden)).And.Contain(" … ");
        snippet.IndexOf(M("Gundren", Gundren), StringComparison.Ordinal).Should().BeLessThan(snippet.IndexOf(" … ", StringComparison.Ordinal));
        snippet.IndexOf(M("Tharden", Tharden), StringComparison.Ordinal).Should().BeGreaterThan(snippet.IndexOf(" … ", StringComparison.Ordinal));
        snippet.Length.Should().BeLessThan(EvidenceSnippet.Window + 20);
        ShouldCutOnWords(snippet, text);
    }

    [Fact]
    public void OneEntryWithNoMention_AnchorsOnTheOther()
    {
        // A block of Gundren's own article mentions only Tharden.
        var text = $"{Words(0, 150)} Brother of {M("Tharden", Tharden)}. {Words(150, 100)}";
        var snippet = Cut(text);
        snippet.Should().Contain(M("Tharden", Tharden)).And.StartWith("…").And.EndWith("…");
    }

    [Fact]
    public void NoMentionAtAll_IsTheStartOfTheText()
    {
        var text = Words(0, 200);
        var snippet = Cut(text);
        snippet.Should().StartWith("word0 word1").And.EndWith("…");
        ShouldCutOnWords(snippet, text);
    }
}
