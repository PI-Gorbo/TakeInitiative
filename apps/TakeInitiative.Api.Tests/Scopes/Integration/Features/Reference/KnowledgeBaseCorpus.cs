using System.Text.Json;

using TakeInitiative.Api.Features.Reference.KnowledgeBase;
using TakeInitiative.KnowledgeBase;
using TakeInitiative.KnowledgeBase.Store;

namespace TakeInitiative.Api.Tests.Integration.Features.Reference;

/// <summary>
/// The synthetic 5eTools corpus the API's tests run against, and the seeding of it into
/// <c>knowledge_base_item</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>Fixtures/5etools-index.json</c> is invented names under the made-up source <c>TST</c>: the 21a
/// script's own fixture output, <b>plus two invented goblins that share their names with the SRD's</b>.
/// Those two are the reason the file is still here rather than deleted with the rest of 21b. They are
/// what "SRD 5.2 ranks above 5eTools at the same rung" is asserted with, and the parser's own fixture
/// corpus (<c>packages/TakeInitiative.KnowledgeBase.Tests/Fixture/</c>) has no such collision, so
/// re-deriving this corpus from it would mean giving that assertion up. No 5eTools data is in the
/// repository.
/// </para>
/// <para>
/// It is read as data and written through <see cref="KnowledgeBaseStore" /> — the same store the ingest
/// CLI uses, against the table the API's own startup created — so what these tests read back is what an
/// ingest would have left, not a hand-rolled approximation of it.
/// </para>
/// </remarks>
internal static class KnowledgeBaseCorpus
{
    /// <summary>The corpus's provider key.</summary>
    public const string Provider = KnowledgeBaseReferenceProvider.ProviderKey;

    /// <summary>How many rows the corpus holds: 10 monsters, 3 spells, 3 items.</summary>
    public const int Count = 16;

    /// <summary>The file, copied beside the test assembly by the project's <c>Fixtures/**/*.json</c> item.</summary>
    public static string Path { get; } =
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "5etools-index.json");

    /// <summary>Every row of the corpus, ready to write.</summary>
    public static IReadOnlyList<KnowledgeBaseRow> Rows { get; } = Read();

    /// <summary>Writes the corpus to <paramref name="connectionString" />.</summary>
    public static Task SeedAsync(string connectionString) =>
        new KnowledgeBaseStore(connectionString).UpsertAsync(Provider, Rows, Guid.NewGuid());

    private static IReadOnlyList<KnowledgeBaseRow> Read()
    {
        var file = JsonSerializer.Deserialize<CorpusFile>(
            File.ReadAllBytes(Path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"{Path} is empty.");

        var items = file.Items ?? throw new InvalidOperationException($"{Path} has no items.");
        var sources = file.Sources ?? new Dictionary<string, string>();
        return [.. items.Select(item => KnowledgeBaseRow.From(Provider, item, sources.GetValueOrDefault(item.Source)))];
    }

    /// <summary>The header the 26b serialiser writes, as much of it as a reader needs.</summary>
    private sealed record CorpusFile(
        IReadOnlyDictionary<string, string>? Sources,
        IReadOnlyList<KnowledgeBaseItem>? Items);
}
