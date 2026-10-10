using System.Text.Json;
using System.Text.Json.Serialization;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>What a completed download left behind, so a later run can recognise it.</summary>
public sealed record FiveEToolsDownloadStamp
{
    /// <summary>The repository it came from, so a cache cannot be read as another corpus.</summary>
    public required string Repository { get; init; }

    /// <summary>The release tag. This is the field the idempotence turns on.</summary>
    public required string Tag { get; init; }

    /// <summary>The archive's name, recorded so the stamp says what it holds.</summary>
    public required string Asset { get; init; }

    /// <summary>How many files were written out of it.</summary>
    public int Files { get; init; }

    /// <summary>When it finished. Written only on success — see <see cref="FiveEToolsDownloadCache" />.</summary>
    public DateTimeOffset CompletedAt { get; init; }
}

/// <summary>
/// The stable place a downloaded 5eTools corpus lives, and the stamp that says which release is in
/// it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The stamp is written last.</b> Nothing else distinguishes "a finished extraction" from "an
/// extraction that was interrupted at file 300 of 523", and a partial corpus is precisely the input
/// that makes a <c>--prune</c> look like a mass deletion. So the stamp is the commit record: no
/// stamp, or a stamp naming another release, means the folder is rebuilt from scratch rather than
/// topped up.
/// </para>
/// <para>
/// <b>Validity is the parser's own requirement, not a second opinion.</b>
/// <see cref="Holds" /> asks <see cref="FiveEToolsParser.FindDataDirectory" /> and
/// <see cref="FiveEToolsParser.RequiredIndexes" />, so a cache this class calls usable cannot be a
/// cache the ingest then rejects as a partial folder.
/// </para>
/// </remarks>
public sealed class FiveEToolsDownloadCache
{
    /// <summary>The extracted corpus, shaped like a checkout: <c>data/</c> and <c>package.json</c>.</summary>
    public const string CheckoutFolder = "checkout";

    /// <summary>The archive, which is deleted as soon as it has been extracted.</summary>
    public const string ArchiveFile = "download.zip";

    /// <summary>The stamp.</summary>
    public const string StampFile = "download.json";

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Where a download goes when nothing says otherwise: a per-user data directory, not the
    /// system temp folder, because a corpus that survives a reboot is the point of recognising it
    /// again.
    /// </summary>
    public static string DefaultRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) is { Length: > 0 } local
            ? local
            : Path.GetTempPath(),
        "takeinitiative",
        "knowledge-base",
        "5etools");

    /// <summary>A cache rooted at <paramref name="root" />, or at <see cref="DefaultRoot" />.</summary>
    public FiveEToolsDownloadCache(string? root = null) =>
        Root = Path.GetFullPath(root is { Length: > 0 } given ? given : DefaultRoot);

    /// <summary>The folder holding the checkout, the archive and the stamp.</summary>
    public string Root { get; }

    /// <summary>The extracted corpus, which is what <c>--from</c> is pointed at.</summary>
    public string Checkout => Path.Combine(Root, CheckoutFolder);

    /// <summary>The archive, mid-download.</summary>
    public string Archive => Path.Combine(Root, ArchiveFile);

    /// <summary>The stamp file.</summary>
    public string Stamp => Path.Combine(Root, StampFile);

    /// <summary>The stamp, or null where there is none and where one cannot be read.</summary>
    /// <remarks>
    /// An unreadable stamp is treated as no stamp rather than as a failure: the answer in both cases
    /// is to download again, and a corrupt file in a cache is not something to make an operator fix
    /// by hand.
    /// </remarks>
    public FiveEToolsDownloadStamp? Read()
    {
        if (!File.Exists(Stamp)) return null;

        try
        {
            return JsonSerializer.Deserialize<FiveEToolsDownloadStamp>(File.ReadAllText(Stamp), Format);
        }
        catch (Exception failure) when (failure is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Records a finished extraction. Called last, after every file is on disk.</summary>
    public void Write(FiveEToolsDownloadStamp stamp)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(Stamp, JsonSerializer.Serialize(stamp, Format));
    }

    /// <summary>
    /// Whether this cache already holds that release of that repository, as a complete corpus.
    /// </summary>
    public bool Holds(string repository, string tag)
    {
        if (Read() is not { } stamp) return false;
        if (!string.Equals(stamp.Repository, repository, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(stamp.Tag, tag, StringComparison.Ordinal)) return false;

        return IsComplete();
    }

    /// <summary>
    /// Whether the checkout is a corpus the ingest would accept — the parser's own two required
    /// indexes, asked through the parser's own folder resolution.
    /// </summary>
    public bool IsComplete()
    {
        try
        {
            var (data, _) = FiveEToolsParser.FindDataDirectory(Checkout);
            return FiveEToolsParser.RequiredIndexes.All(relative => File.Exists(Path.Combine(data, relative)));
        }
        catch (FiveEToolsBuildException)
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes the whole cache — checkout, archive and stamp. This is what "delete it after the
    /// work is done" means, and what a release mismatch does before downloading the new one.
    /// </summary>
    public void Clear()
    {
        if (!Directory.Exists(Root)) return;

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Already gone.
        }
    }
}
