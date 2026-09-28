using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// SRD 5.2's monsters, read from the resources embedded in the API (20a.3):
/// <c>Reference/Srd52/monsters.json</c> and <c>source.json</c>, written by
/// <c>scripts/srd/build-srd52.mjs</c>. A singleton that reads them once, on first use, and holds
/// the monsters, a dictionary by id and each name folded for matching. Nothing reaches the network
/// (invariant 10). <c>Program</c> calls <see cref="EnsureLoaded"/> after build, so a missing or
/// broken resource fails startup rather than the first search.
/// </summary>
public class SrdCatalog
{
    public const string ProviderKey = "srd52";
    public const string MonstersResource = "TakeInitiative.Api.Reference.Srd52.monsters.json";
    public const string SourceResource = "TakeInitiative.Api.Reference.Srd52.source.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Lazy<Loaded> loaded = new(Load);

    private sealed record Loaded(
        IReadOnlyList<SrdMonster> Monsters,
        IReadOnlyDictionary<string, SrdMonster> ById,
        IReadOnlyList<ReferenceMatcher.Candidate<SrdMonster>> Candidates,
        SrdSource Source,
        ReferenceAttribution Attribution);

    /// <summary>Every monster, in name order.</summary>
    public IReadOnlyList<SrdMonster> Monsters => loaded.Value.Monsters;

    /// <summary>Each monster with its folded name, ready for <see cref="ReferenceMatcher.Search{T}"/>.</summary>
    public IReadOnlyList<ReferenceMatcher.Candidate<SrdMonster>> Candidates => loaded.Value.Candidates;

    public SrdSource Source => loaded.Value.Source;
    public ReferenceAttribution Attribution => loaded.Value.Attribution;

    /// <summary>The monster with this id ("goblin-warrior"), or null.</summary>
    public SrdMonster? Get(string id) => loaded.Value.ById.GetValueOrDefault(id);

    /// <summary>Reads the resources now if they have not been read. Throws if they are missing or malformed.</summary>
    public void EnsureLoaded() => _ = loaded.Value;

    private static Loaded Load()
    {
        var source = Read<SrdSource>(SourceResource);
        var blocks = Read<StatBlock[]>(MonstersResource);
        if (blocks.Length != source.Count)
        {
            throw new InvalidOperationException($"{MonstersResource} has {blocks.Length} monsters, but source.json says {source.Count}.");
        }

        var monsters = blocks.Select(block => new SrdMonster(block, ReferenceMatcher.Fold(block.Name), Summarise(block))).ToList();
        var byId = monsters.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var candidates = monsters.Select(m => new ReferenceMatcher.Candidate<SrdMonster>(m, m.Name, m.FoldedName)).ToList();
        var attribution = new ReferenceAttribution(source.Attribution, source.License, source.LicenseUrl, source.SourceUrl);
        return new Loaded(monsters, byId, candidates, source, attribution);
    }

    /// <summary>What a search row and + Wiki show of a monster: "CR 1/4 · Small Fey", a Character, and its Stats.</summary>
    public static ReferenceSummary Summarise(StatBlock block) => new(
        Provider: ProviderKey,
        Id: block.Id,
        Name: block.Name,
        Category: ReferenceCategory.Monster,
        Detail: $"CR {block.Cr} · {block.Size} {block.Type}",
        Url: null,
        SuggestedKind: EntryKind.Character,
        Stats: ReferenceStats.From(block));

    private static T Read<T>(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"The embedded resource {resource} is missing. Run `pnpm srd:build` and check the csproj's EmbeddedResource.");
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"The embedded resource {resource} is empty.");
    }
}
