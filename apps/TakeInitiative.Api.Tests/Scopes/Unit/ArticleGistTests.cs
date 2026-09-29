using FluentAssertions;
using TakeInitiative.Api.Features.Entries;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>The summary gist's text rule (25g): first paragraph, plain text, capped.</summary>
public class ArticleGistTests
{
    [Theory]
    [InlineData("", null)]
    [InlineData("   \n\n ", null)]
    [InlineData("Plain.", "Plain.")]
    [InlineData("First *line*\nsame paragraph.\n\nSecond.", "First line same paragraph.")]
    [InlineData("# Heading\n\nBody.", "Heading")]
    [InlineData("- item one\n- item two", "item one")]
    [InlineData("Use `code` and \\*stars\\*.", "Use code and *stars*.")]
    [InlineData("A [link](https://example.com) here.", "A link here.")]
    [InlineData("![map](https://example.com/map.png)\n\nThe keep.", "The keep.")]
    [InlineData("Mail me@example.com", "Mail me@example.com")]
    public void Of_TakesTheFirstParagraphAsPlainText(string markdown, string? expected)
        => ArticleGist.Of(markdown).Should().Be(expected);

    [Fact]
    public void Of_ReadsAMentionAsItsText()
        => ArticleGist.Of($"Brother of @[Tharden **Rockseeker**](entry:{Guid.NewGuid()}).")
            .Should().Be("Brother of Tharden Rockseeker.");

    [Fact]
    public void Of_CapsLongTextOnAWord_WithAnEllipsis()
    {
        var text = string.Join(' ', Enumerable.Range(0, 60).Select(i => $"word{i}"));
        var gist = ArticleGist.Of(text)!;
        gist.Length.Should().BeLessThanOrEqualTo(ArticleGist.MaxLength);
        gist.Should().EndWith("…");
        text.Should().StartWith(gist.TrimEnd('…'));
        gist.TrimEnd('…').Split(' ').Last().Should().MatchRegex(@"^word\d+$");
        text.Split(' ').Should().Contain(gist.TrimEnd('…').Split(' ').Last());
    }
}
