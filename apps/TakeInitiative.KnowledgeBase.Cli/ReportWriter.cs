using System.Globalization;

using TakeInitiative.KnowledgeBase.FiveETools;
using TakeInitiative.KnowledgeBase.Store;

namespace TakeInitiative.KnowledgeBase.Cli;

/// <summary>
/// Everything the ingest prints. It is one class so that the whole of what an operator sees can be
/// read in one place, and so that the diff is reported in exactly the same words whether or not the
/// run goes on to write.
/// </summary>
/// <param name="output">Where the report goes.</param>
/// <param name="error">Where a failure goes, so a script can separate them.</param>
public sealed class ReportWriter(TextWriter output, TextWriter error)
{
    /// <summary>Labels are padded to this, so the counts line up under each other.</summary>
    private const int LabelWidth = 9;

    /// <summary>And the counts are right-aligned in this, which fits a six-figure corpus.</summary>
    private const int CountWidth = 7;

    /// <summary>The header: which provider, and which folder it is reading.</summary>
    public void Header(string provider, string source) => output.WriteLine($"{provider} · {source}");

    /// <summary>
    /// One line per data file as the parse reads it. A 5eTools checkout is a few hundred files and a
    /// full parse takes the better part of a minute; without this there is no way to tell a slow read
    /// from a hang. The path is relative to the folder the operator named, because the absolute one is
    /// already in the header.
    /// </summary>
    public void File(string relativePath) => output.WriteLine($"  read      {relativePath}");

    /// <summary>What the parse skipped, and why. Only the counts that are not zero.</summary>
    public void Skipped(FiveEToolsBuildReport report)
    {
        var notes = new List<string>();
        if (report.Skipped.Ua > 0) notes.Add($"{Number(report.Skipped.Ua)} Unearthed Arcana");
        if (report.Skipped.Srd52 > 0) notes.Add($"{Number(report.Skipped.Srd52)} already in the SRD");
        if (report.Skipped.MissingBase.Count > 0) notes.Add($"{Number(report.Skipped.MissingBase.Count)} missing a _copy base");
        if (report.Skipped.Broken.Count > 0) notes.Add($"{Number(report.Skipped.Broken.Count)} with no hit points");
        if (report.Skipped.NoName > 0) notes.Add($"{Number(report.Skipped.NoName)} with no name or source");

        if (notes.Count > 0) output.WriteLine($"  skipped   {string.Join(" · ", notes)}");
        if (report.WithoutStats > 0) output.WriteLine($"  no stats  {Number(report.WithoutStats)} monsters");
    }

    /// <summary>
    /// The diff, in the plan's format. Printed before anything is written, on every run, so a dry run
    /// and a real run report the same thing in the same words.
    /// </summary>
    public void Diff(IngestReport report, FiveEToolsCounts counts)
    {
        output.WriteLine(
            $"{Line("parsed", report.Parsed)} items  ({Number(counts.Monster)} monsters · "
            + $"{Number(counts.Spell)} spells · {Number(counts.Item)} items)");
        output.WriteLine(Line("new", report.New));
        output.WriteLine(Line("updated", report.Updated));
        output.WriteLine(Line("unchanged", report.Unchanged));
        output.WriteLine($"{Line("missing", report.Missing)}   (in the database, absent from this source)");
    }

    /// <summary>A dry run, saying plainly that it did nothing.</summary>
    public void DryRun() => output.WriteLine("  dry run · nothing was written");

    /// <summary>What a write did.</summary>
    public void Written(IngestReport report, Guid batch)
    {
        if (!report.HasWrites)
        {
            output.WriteLine("  written · nothing to write; every row is already stored as parsed");
            return;
        }

        output.WriteLine(
            $"  written · {Number(report.New)} inserted, {Number(report.Updated)} updated  (batch {batch})");
    }

    /// <summary>
    /// The warning a plain run gives when the source is missing rows the database has. It is the
    /// whole reason additive-by-default is the default: this is what pointing the tool at half a
    /// download looks like, and the answer is a warning rather than a deletion.
    /// </summary>
    public void NothingDeleted(IngestReport report)
    {
        if (report.Missing == 0) return;

        output.WriteLine(
            $"  warning · {Number(report.Missing)} stored rows are absent from this source, and none of "
            + "them were deleted.");
        output.WriteLine(
            "            A plain run is additive. --prune is what removes them, and it refuses on more "
            + "than 20%.");
    }

    /// <summary>What a prune did.</summary>
    public void Pruned(PruneReport prune)
    {
        if (prune.Considered == 0)
        {
            output.WriteLine("  pruned  · nothing to prune; the source has every stored row");
            return;
        }

        output.WriteLine($"  pruned  · {Number(prune.Deleted)} deleted");
        if (prune.MarkedStale > 0)
        {
            output.WriteLine(
                $"            {Number(prune.MarkedStale)} kept and marked stale, because an entry links "
                + "to them");
        }
    }

    /// <summary>
    /// The refusal, and the reason it exists. It goes to stderr with the exit code, because it is a
    /// failure: the operator asked for something and did not get it.
    /// </summary>
    public void PruneRefused(PruneReport prune)
    {
        var share = (prune.Share * 100).ToString("0.0", CultureInfo.InvariantCulture);

        error.WriteLine(
            $"  refused · --prune would remove {Number(prune.Considered)} of {Number(prune.StoredBefore)} "
            + $"rows ({share}%), over the 20% limit.");
        error.WriteLine(
            "            Removing most of a corpus is what pointing --from at a partial download or the "
            + "wrong");
        error.WriteLine(
            "            folder looks like. Nothing was written at all — not even the inserts and "
            + "updates above.");
        error.WriteLine(
            "            Check the folder first. If the source really did shrink, re-run with --prune "
            + "--force.");
    }

    /// <summary>
    /// A parse that could not be trusted. The message comes from the parser and names the file, and
    /// <see cref="PartialFolder" /> is what explains the two cases where the file alone is not enough.
    /// </summary>
    public void ParseFailed(string message)
    {
        error.WriteLine($"  error   · {message}");
        error.WriteLine("            Nothing was written.");
    }

    /// <summary>
    /// The one parse failure whose own message does not explain itself: a missing
    /// <c>bestiary/index.json</c> or <c>spells/index.json</c>.
    /// </summary>
    /// <remarks>
    /// 5eTools' data folder is asymmetric, and reproducing that asymmetry was a deliberate decision in
    /// 26b: the bestiary's and the spells' <c>index.json</c> are required, while <c>items.json</c>,
    /// <c>books.json</c> and <c>adventures.json</c> are optional and simply yield nothing. So "cannot
    /// read …/spells/index.json" is technically complete and practically useless — it reads like a
    /// missing optional file when it is in fact the signature of a folder that is not a whole
    /// checkout. This says which folder is short and what that usually means.
    /// </remarks>
    public void PartialFolder(string dataDirectory, IReadOnlyList<string> missing)
    {
        error.WriteLine(
            $"  error   · {dataDirectory} is not a complete 5eTools data folder.");
        foreach (var file in missing) error.WriteLine($"            {file} is missing.");
        error.WriteLine(
            "            bestiary/ and spells/ each carry an index.json listing their per-source files, "
            + "and");
        error.WriteLine(
            "            both are required. items.json, items-base.json, books.json and adventures.json "
            + "are");
        error.WriteLine(
            "            optional, so a missing index is almost always --from pointing at a partial");
        error.WriteLine(
            "            download, an unpacked subfolder, or a different folder entirely.");
        error.WriteLine("            Nothing was written.");
    }

    /// <summary>
    /// There is no table to write to. The CLI does not create one: the API owns the schema, so that
    /// there is one owner of it rather than two that have to agree.
    /// </summary>
    public void SchemaMissing(string message)
    {
        error.WriteLine($"  error   · {message}");
        error.WriteLine(
            "            The API creates this table when it starts, and the ingest deliberately does "
            + "not.");
        error.WriteLine(
            "            Start the API once against this database and run the ingest again:");
        error.WriteLine(
            "              docker compose -p takeinitiative -f compose.dev.yml up -d postgres");
        error.WriteLine(
            "              dotnet run --project apps/TakeInitiative.Api");
        error.WriteLine("            Nothing was written.");
    }

    /// <summary>The database could not be reached at all.</summary>
    public void ConnectionFailed(string message)
    {
        error.WriteLine($"  error   · cannot reach the database: {message}");
        error.WriteLine(
            "            Pass --connection, or set KnowledgeBase__ConnectionString. The default is the "
            + "dev");
        error.WriteLine("            database on localhost:7401. Nothing was written.");
    }

    /// <summary>A label and its count, padded so a column of them lines up.</summary>
    private static string Line(string label, int count) =>
        $"  {label.PadRight(LabelWidth)}{Number(count).PadLeft(CountWidth)}";

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
