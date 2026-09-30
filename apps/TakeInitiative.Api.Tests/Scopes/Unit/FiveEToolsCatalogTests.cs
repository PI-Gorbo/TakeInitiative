using FluentAssertions;
using Microsoft.Extensions.Configuration;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Api.Tests.Integration;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// The 5eTools index (21b.2, 21b.3), read from the synthetic <c>Fixtures/5etools-index.json</c>:
/// invented names under the made-up source TST, built by the 21a script from its fixture plus two
/// invented goblins that share names with the SRD's. No 5eTools data is in the repo.
/// </summary>
public class FiveEToolsCatalogTests : IDisposable
{
    private static readonly FiveEToolsCatalog Catalog = new(FiveEToolsFixture.IndexPath);
    private readonly DiceRoller roller = new(new Random(1234));
    private readonly string temp = Path.Combine(Path.GetTempPath(), $"5etools-{Guid.NewGuid():N}");

    public FiveEToolsCatalogTests() => Directory.CreateDirectory(temp);

    public void Dispose() => Directory.Delete(temp, recursive: true);

    private string Write(string json)
    {
        var path = Path.Combine(temp, "index.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void TheIndex_Loads_WithEveryCategory()
    {
        Catalog.IsOn.Should().BeTrue();
        Catalog.Rows.Should().HaveCount(16);
        Catalog.Rows.GroupBy(r => r.Category).ToDictionary(g => g.Key, g => g.Count()).Should().BeEquivalentTo(
            new Dictionary<ReferenceCategory, int> { [ReferenceCategory.Monster] = 10, [ReferenceCategory.Spell] = 3, [ReferenceCategory.Item] = 3 });
        Catalog.BaseUrl.Should().Be("https://5e.tools/");
        Catalog.Attribution.SourceUrl.Should().Be("https://5e.tools/");
        Catalog.Attribution.LicenseName.Should().BeEmpty("5eTools is not a licence the app credits; the row only links out");
    }

    [Fact]
    public void EveryMonstersStats_PassTheDiceChecker()
    {
        var withStats = Catalog.Rows.Where(r => r.Stats is not null).Select(r => Catalog.Find(r.Id)!).ToList();
        withStats.Should().NotBeEmpty();
        withStats.Should().OnlyContain(s => s.Category == ReferenceCategory.Monster);
        foreach (var summary in withStats)
        {
            var stats = summary.Stats!;
            foreach (var expression in new[] { stats.InitiativeRoll!, stats.MaxHp! })
            {
                var check = roller.Check(expression);
                check.IsSuccess.Should().BeTrue($"{summary.Id}: {expression} {(check.IsFailure ? check.Error : "")}");
            }
            stats.Ac.Should().BeInRange(1, Stats.AcMax);
        }
    }

    [Fact]
    public void ASummary_HoldsOnlyIndexFields()
    {
        Catalog.Find("monster_test-gremlin_tst").Should().Be(new ReferenceSummary(
            Provider: "5etools",
            Id: "monster_test-gremlin_tst",
            Name: "Test Gremlin",
            Category: ReferenceCategory.Monster,
            Detail: "CR 1/2 · Small Fey · TST",
            Url: "https://5e.tools/bestiary.html#test%20gremlin_tst",
            SuggestedKind: EntryKind.Character,
            Stats: Stats.Of("1d20+2", "3d6+3", 15),
            Book: "TST p. 12",
            BookTitle: "Test Book of Beasts"));
        Catalog.Find("monster_test-gremlin-zombie_tsta")!.BookTitle.Should().Be("Test Adventure in the Lint Caves", "the title comes from the index's sources");
        FiveEToolsCatalog.Summarise(Catalog.Rows[0]).BookTitle.Should().BeNull("a source the index does not name has no title");

        Catalog.Find("monster_goblin-tinkerer_tst")!.Stats!.InitiativeRoll.Should().Be("1d20-1");
        Catalog.Find("monster_test-mossback_tst")!.Should().Match<ReferenceSummary>(s => s.Stats == null && s.Book == "TST");
        Catalog.Find("spell_test-sparkburst_tst")!.Should().Match<ReferenceSummary>(s =>
            s.Category == ReferenceCategory.Spell && s.SuggestedKind == EntryKind.Other && s.Stats == null
            && s.Detail == "Level 3 Evocation · TST");
        Catalog.Find("item_test-satchel-of-plenty_tst")!.Should().Match<ReferenceSummary>(s =>
            s.Category == ReferenceCategory.Item && s.SuggestedKind == EntryKind.Item && s.Stats == null);
        Catalog.Find("goblin-warrior").Should().BeNull();
    }

    [Fact]
    public async Task TheProvider_IsSearchOnly()
    {
        var provider = new FiveEToolsReferenceProvider(Catalog);
        (provider.Key, provider.Label, provider.HasStatBlocks).Should().Be(("5etools", "5eTools", false));

        (await provider.Search("gremlin", 10, CancellationToken.None)).Select(m => m.Item.Name).Should().StartWith("Test Gremlin");
        (await provider.Search("sparkbrst", 5, CancellationToken.None)).Select(m => m.Item.Id).Should().Contain("spell_test-sparkburst_tst", "the fuzzy rung");
        (await provider.Get("monster_test-gremlin_tst", CancellationToken.None)).Should().BeNull("the app never shows 5eTools content");
        (await provider.Find("monster_test-gremlin_tst", CancellationToken.None))!.Name.Should().Be("Test Gremlin");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/nowhere/5etools/index.json")]
    public async Task NoPathOrNoFile_IsOff_WithoutAnError(string? path)
    {
        var catalog = new FiveEToolsCatalog(path);
        catalog.IsOn.Should().BeFalse();
        catalog.Rows.Should().BeEmpty();
        var provider = new FiveEToolsReferenceProvider(catalog);
        (await provider.Search("gremlin", 10, CancellationToken.None)).Should().BeEmpty();
        (await provider.Find("monster_test-gremlin_tst", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public void ARelativePath_IsResolvedFromTheContentRoot()
    {
        File.Copy(FiveEToolsFixture.IndexPath, Path.Combine(temp, "index.json"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Reference:FiveETools:IndexPath"] = "index.json" })
            .Build();
        FiveEToolsCatalog.FromConfiguration(configuration, temp, null).Rows.Should().HaveCount(16);
        FiveEToolsCatalog.FromConfiguration(null, temp, null).IsOn.Should().BeFalse();
    }

    [Theory]
    [InlineData("{\"format\":2,\"items\":[]}")]
    [InlineData("{\"items\":[]}")]
    [InlineData("{\"format\":1,\"items\":[{\"id\":\"x\"")]
    [InlineData("{\"format\":1,\"items\":[{\"id\":\"x\",\"name\":\"X\",\"category\":\"Feat\",\"source\":\"TST\",\"page\":1,\"url\":\"https://5e.tools/x\",\"label\":null,\"stats\":null}]}")]
    [InlineData("{\"format\":1,\"items\":[{\"id\":\"x\",\"name\":\"X\",\"category\":\"Spell\",\"source\":\"TST\",\"page\":1,\"url\":\"https://5e.tools/x\",\"label\":null,\"stats\":null},{\"id\":\"x\",\"name\":\"Y\",\"category\":\"Spell\",\"source\":\"TST\",\"page\":1,\"url\":\"https://5e.tools/y\",\"label\":null,\"stats\":null}]}")]
    [InlineData("{\"format\":1}")]
    public void AFileThatIsThereButBad_Throws(string json)
    {
        var path = Write(json);
        var act = () => new FiveEToolsCatalog(path);
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{path}*");
    }
}
