using System.Text.Json;

using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// The corpus the parser is tested against, and the shorthands the ported cases use.
/// </summary>
/// <remarks>
/// <para>
/// <c>Fixture/</c> is invented data shaped like a 5eTools source checkout, under the made-up source
/// <c>TST</c>. It is a copy of <c>scripts/5etools/fixture/</c>, taken when the parser was ported
/// (roadmap step 26b) so that the corpus survives 26d deleting that folder. Never add a real
/// 5eTools file to it, not even an excerpt.
/// </para>
/// <para>
/// The fixtures deliberately exercise the shapes that took work to understand: <c>_copy</c>
/// inheritance with and without <c>_mod</c>, a copy across two files, a copy carrying
/// <c>_templates</c>, a copy whose base is missing, an SRD 5.2 duplicate, a UA source, a monster
/// with no hit points and no <c>_copy</c>, a <c>{ special }</c> AC, a <c>{ special }</c> hit-point
/// block, a CR the parser does not know, a two-size monster, a <c>{ choose }</c> creature type, and
/// creature-type tags that must not reach the output.
/// </para>
/// </remarks>
internal static class Fixture5eTools
{
    /// <summary>The fixture checkout — the folder holding <c>data/</c> and <c>package.json</c>.</summary>
    public static string Checkout { get; } = Path.Combine(AppContext.BaseDirectory, "Fixture");

    /// <summary>The fixture's <c>data/</c> folder.</summary>
    public static string Data { get; } = Path.Combine(Checkout, "data");

    /// <summary>
    /// The committed output of the Node script for this corpus. See <c>GoldenFileTests</c> for the
    /// command that produced it.
    /// </summary>
    public static string GoldenFile { get; } =
        Path.Combine(AppContext.BaseDirectory, "Golden", "fixture-index.json");

    /// <summary>
    /// The fixture parsed, with the monster floor lowered to 1. The Node tests pass the same, for
    /// the same reason: eight monsters is a real corpus here and a wrong folder in production.
    /// </summary>
    public static FiveEToolsBuildResult Build(bool noStats = false, string? baseUrl = null, string? from = null) =>
        FiveEToolsParser.Build(new FiveEToolsParserOptions
        {
            From = from ?? Checkout,
            MinMonsters = 1,
            NoStats = noStats,
            BaseUrl = baseUrl ?? FiveEToolsParserOptions.DefaultBaseUrl,
        });

    /// <summary>The row with this id, failing the test when there is none.</summary>
    public static KnowledgeBaseItem Row(this FiveEToolsIndex index, string id)
    {
        var found = index.Items.SingleOrDefault(item => item.Id == id);
        Assert.NotNull(found);
        return found;
    }

    /// <summary>
    /// A JSON literal as an element, for the cases that test one reader against one shape the way
    /// the Node tests pass an object literal.
    /// </summary>
    /// <remarks>
    /// The documents are rooted for the life of the test run rather than disposed: a
    /// <see cref="JsonElement" /> is a window onto its <see cref="JsonDocument" /> and stops working
    /// the moment that document goes, which would make these helpers a trap.
    /// </remarks>
    public static JsonElement Json(string json)
    {
        var document = JsonDocument.Parse(json);
        lock (Documents) Documents.Add(document);
        return document.RootElement;
    }

    /// <summary>A monster's <c>_copy</c>-able fields, straight from a JSON literal.</summary>
    public static MonsterFields Monster(string json) => MonsterFields.From(Json(json));

    private static readonly List<JsonDocument> Documents = [];
}
