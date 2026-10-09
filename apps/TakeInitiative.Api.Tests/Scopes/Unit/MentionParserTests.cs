using System.Text.Json;
using FluentAssertions;
using TakeInitiative.Api.Features.Entries;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// <see cref="MentionParser"/> against <c>Fixtures/mentions.json</c>, the case list the
/// web's <c>mentionMarkdown.test.ts</c> also runs, so the two parsers agree.
/// </summary>
public class MentionParserTests
{
    private record FixtureMention(string EntryId, string Text);
    private record FixtureCase(string Name, string Text, FixtureMention[] Mentions);
    private record Fixture(FixtureCase[] Cases);

    private static readonly Fixture Cases = JsonSerializer.Deserialize<Fixture>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mentions.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    public static TheoryData<string> CaseNames()
    {
        var names = new TheoryData<string>();
        foreach (var c in Cases.Cases) names.Add(c.Name);
        return names;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void ParsesTheSharedCase(string name)
    {
        var c = Cases.Cases.Single(x => x.Name == name);

        var mentions = MentionParser.Parse(c.Text);

        mentions.Select(m => (m.EntryId.ToString(), m.Text))
            .Should().Equal(c.Mentions.Select(m => (m.EntryId, m.Text)));
    }

    [Fact]
    public void EntryIdsAreDistinctInOrderOfFirstMention()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        MentionParser.EntryIds($"@[B](entry:{b}) @[A](entry:{a}) @[B again](entry:{b})")
            .Should().Equal(b, a);
    }

    [Fact]
    public void TextWithoutMentionsHasNone()
    {
        MentionParser.EntryIds("Just a note with [a link](https://example.com).").Should().BeEmpty();
        MentionParser.EntryIds("").Should().BeEmpty();
    }
}
