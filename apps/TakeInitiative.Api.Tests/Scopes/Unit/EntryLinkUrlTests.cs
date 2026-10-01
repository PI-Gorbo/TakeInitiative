using FluentAssertions;

using TakeInitiative.Api.Features.Entries;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// <see cref="EntryLinkUrl"/> (27c): the scheme allowlist that keeps a <c>javascript:</c> url out of
/// the database, and the normal form the duplicate check compares in.
/// </summary>
/// <remarks>
/// These are the rules the <c>POST links</c> validator calls, tested without a host because they are
/// decisions about a string. <c>LinkApiTests</c> asserts the same rules through the endpoint, which is
/// what proves the validator is where they are applied.
/// </remarks>
public class EntryLinkUrlTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/a/b?c=d#e")]
    [InlineData("  https://example.com/padded  ")]
    [InlineData("HTTPS://EXAMPLE.COM/Shouty")]
    public void AnHttpOrHttpsUrl_IsAllowed(string url) => EntryLinkUrl.IsAllowed(url).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com")]
    [InlineData("mailto:a@b.com")]
    [InlineData("//example.com/protocol-relative")]
    [InlineData("/relative")]
    [InlineData("example.com")]
    public void EverythingElse_IsRefused(string? url) => EntryLinkUrl.IsAllowed(url).Should().BeFalse();

    [Fact]
    public void AUrlLongerThanTheCap_IsRefused()
    {
        var atTheCap = "https://example.com/" + new string('a', EntryLinks.UrlMaxLength - "https://example.com/".Length);
        atTheCap.Length.Should().Be(EntryLinks.UrlMaxLength);

        EntryLinkUrl.IsAllowed(atTheCap).Should().BeTrue();
        EntryLinkUrl.IsAllowed(atTheCap + "a").Should().BeFalse();
    }

    [Theory]
    // The scheme and the host fold, because neither is case-sensitive.
    [InlineData("HTTPS://Example.COM/path", "https://example.com/path")]
    // One trailing slash on the path goes, so a pasted url and a typed one are one link.
    [InlineData("https://example.com/path/", "https://example.com/path")]
    [InlineData("https://example.com/", "https://example.com/")]
    // The query and the fragment are kept exactly, and so is the path's case.
    [InlineData("https://example.com/Path?b=2&a=1#Frag", "https://example.com/Path?b=2&a=1#Frag")]
    public void Normalize_FoldsOnlyWhatIsSafeToFold(string url, string expected)
        => EntryLinkUrl.Normalize(url).Should().Be(expected);

    [Fact]
    public void TwoUrlsThatDifferOnlyInHostCaseOrATrailingSlash_AreTheSameLink()
        => EntryLinkUrl.Normalize("https://Example.com/Sheet/")
            .Should().Be(EntryLinkUrl.Normalize("https://example.com/Sheet"));

    [Fact]
    public void AUrlWithADifferentPathCase_IsADifferentLink()
        => EntryLinkUrl.Normalize("https://example.com/Sheet")
            .Should().NotBe(EntryLinkUrl.Normalize("https://example.com/sheet"));

    [Fact]
    public void ARefusedUrl_NormalizesToNull() => EntryLinkUrl.Normalize("javascript:alert(1)").Should().BeNull();
}
