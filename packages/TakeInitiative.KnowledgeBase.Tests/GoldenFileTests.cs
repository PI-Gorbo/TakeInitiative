using System.Text;

using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// The port's proof: this parser writes the same bytes the Node script wrote.
/// </summary>
/// <remarks>
/// <para>
/// <c>scripts/5etools/build-5etools-index.mjs</c> is the specification for
/// <see cref="FiveEToolsParser" />, and 438 lines of it encode 5eTools' JSON idioms — <c>_copy</c>
/// inheritance above all — that took real work to arrive at and are not obvious from their data.
/// Capturing the script's output first turns "reimplement a parser for a format we half-remember"
/// into "match a known-good output", which is a question a test can answer.
/// </para>
/// <para>
/// <c>Golden/fixture-index.json</c> was produced, before a line of this package was written, by:
/// </para>
/// <code>
/// pnpm 5etools:build --from scripts/5etools/fixture/data \
///     --out packages/TakeInitiative.KnowledgeBase.Tests/Golden/fixture-index.json \
///     --min-monsters 1
/// </code>
/// <para>
/// Re-derive it with exactly that command while the script is still in the tree. The script has no
/// timestamp in its output by design, so the command is idempotent.
/// </para>
/// </remarks>
public class GoldenFileTests
{
    /// <summary>
    /// Byte equality, not a canonical form. The golden file's exact bytes are the contract: 26c
    /// decides what an ingest changed by hashing a row's serialised form, so a difference in key
    /// order, number formatting or string escaping is a difference that matters, and normalising
    /// both sides through a JSON parser would hide precisely the bugs this test exists to catch.
    /// </summary>
    [Fact]
    public void The_parse_matches_the_Node_script_byte_for_byte()
    {
        var index = Fixture5eTools.Build(from: Fixture5eTools.Data).Index;

        var written = Encoding.UTF8.GetBytes(FiveEToolsIndexSerializer.Serialize(index));
        var golden = File.ReadAllBytes(Fixture5eTools.GoldenFile);

        // Compared as text first: a mismatch in the bytes is unreadable, and every difference the
        // port could plausibly have is visible in the string.
        Assert.Equal(
            File.ReadAllText(Fixture5eTools.GoldenFile, Encoding.UTF8),
            FiveEToolsIndexSerializer.Serialize(index));
        Assert.Equal(golden, written);
    }

    /// <summary>
    /// The layout the script writes: the header on one line, then one row per line, then a trailing
    /// newline. It is what makes a diff between two builds readable and a row hashable on its own.
    /// </summary>
    [Fact]
    public void The_file_is_one_row_per_line()
    {
        var text = FiveEToolsIndexSerializer.Serialize(Fixture5eTools.Build().Index);

        Assert.Equal(14, Fixture5eTools.Build().Index.Items.Count);
        Assert.Equal(17, text.Split('\n').Length);
        Assert.EndsWith("\n]}\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two parses of the same folder serialise to the same string, character for character.
    /// </summary>
    /// <remarks>
    /// This is the property 26c's diff rests on. Rows are compared by a content hash so that
    /// <c>updated</c> means something; if the parse were not byte-stable — a dictionary iterated in
    /// hash order, a number formatted under the current culture, a sort that is not total — every
    /// run would report every row as updated, the diff would become noise, and the prune threshold
    /// that reads it would stop being a safety rail.
    /// </remarks>
    [Fact]
    public void Two_parses_of_the_same_folder_serialise_identically()
    {
        var first = FiveEToolsIndexSerializer.Serialize(Fixture5eTools.Build().Index);
        var second = FiveEToolsIndexSerializer.Serialize(Fixture5eTools.Build().Index);

        Assert.Equal(first, second);
    }

    /// <summary>
    /// The two ways of naming the folder — the checkout or its <c>data/</c> — read the same data and
    /// find the same <c>package.json</c>, so they build the same index.
    /// </summary>
    [Fact]
    public void A_checkout_and_its_data_folder_build_the_same_index()
    {
        var fromCheckout = FiveEToolsIndexSerializer.Serialize(Fixture5eTools.Build().Index);
        var fromData = FiveEToolsIndexSerializer.Serialize(Fixture5eTools.Build(from: Fixture5eTools.Data).Index);

        Assert.Equal(fromCheckout, fromData);
    }
}
