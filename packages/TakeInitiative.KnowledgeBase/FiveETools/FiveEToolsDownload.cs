using System.IO.Compression;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// Where a release archive comes from. Implemented by the CLI, so the HTTP lives next to the
/// command that does it and this package stays a parser with no network of its own.
/// </summary>
public interface IFiveEToolsArchiveSource
{
    /// <summary>
    /// The forge's answer for "the latest release of this repository", as the raw document, which
    /// <see cref="FiveEToolsRelease.Parse" /> reads. Raw rather than parsed so that the shape of
    /// that document is pinned by a test in this package rather than by the HTTP call.
    /// </summary>
    Task<string> LatestReleaseAsync(string repository, CancellationToken cancellationToken);

    /// <summary>Fetches <paramref name="url" /> to <paramref name="destination" />.</summary>
    Task DownloadAsync(Uri url, string destination, CancellationToken cancellationToken);
}

/// <summary>What a download run needs, and what it reports as it goes.</summary>
public sealed record FiveEToolsDownloadOptions
{
    /// <summary>
    /// The repository to take the latest release of, as <c>owner/name</c>.
    /// </summary>
    /// <remarks>
    /// <b>There is deliberately no default</b>, and the one in this repository's committed
    /// configuration is deliberately empty. See step 26's decision 5 and its 2026-10-10 amendment:
    /// the capability is a general "fetch a release archive and unpack it", and which corpus it is
    /// pointed at is the operator's to state, so nothing committed here names someone else's
    /// content as a thing to go and get.
    /// </remarks>
    public required string Repository { get; init; }

    /// <summary>Called with the release once it is known, before anything is fetched.</summary>
    public Action<FiveEToolsRelease>? OnResolved { get; init; }

    /// <summary>Called instead of downloading, when the cache already holds that release.</summary>
    public Action<FiveEToolsRelease>? OnReused { get; init; }

    /// <summary>Called just before the archive is fetched.</summary>
    public Action<FiveEToolsRelease>? OnDownloading { get; init; }

    /// <summary>Called with the number of files written out of the archive.</summary>
    public Action<int>? OnExtracted { get; init; }
}

/// <summary>What a download produced.</summary>
/// <param name="Checkout">The folder to pass as <c>--from</c>.</param>
/// <param name="Release">Which release it is.</param>
/// <param name="Reused">Whether it was already on disk, and so nothing was fetched.</param>
public sealed record FiveEToolsDownloadResult(string Checkout, FiveEToolsRelease Release, bool Reused);

/// <summary>
/// Fetches the latest release of a 5eTools source repository into a stable local folder, and
/// recognises one it already has.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only the data is written to disk.</b> The archive holds the whole site — HTML, scripts,
/// styles, service worker — and <see cref="Wanted" /> takes <c>data/</c> and the checkout's
/// <c>package.json</c> and nothing else. That is 120 MB of the 157 MB in a current release, but
/// the reason is not the 37 MB: what the ingest needs is the data, so their site is not something
/// this tool should be putting on an operator's machine, let alone serving from it.
/// </para>
/// <para>
/// <b>Reuse is by release tag.</b> A tag never moves, so "do I already have this?" is a string
/// comparison against <see cref="FiveEToolsDownloadCache" />'s stamp plus a check that the corpus
/// on disk is complete. A match fetches nothing at all; a mismatch deletes what is there and
/// starts again, because a half-replaced corpus is worse than either.
/// </para>
/// </remarks>
public sealed class FiveEToolsDownload(IFiveEToolsArchiveSource source, FiveEToolsDownloadCache cache)
{
    /// <summary>The cache this writes into, so a caller can clean it up afterwards.</summary>
    public FiveEToolsDownloadCache Cache { get; } = cache;

    /// <summary>
    /// Resolves the latest release, reuses the cache if it already holds it, and otherwise fetches
    /// and unpacks it.
    /// </summary>
    /// <exception cref="FiveEToolsBuildException">
    /// The repository is not named, the forge's answer is not a release, or the archive is not one
    /// this will unpack.
    /// </exception>
    public async Task<FiveEToolsDownloadResult> RunAsync(
        FiveEToolsDownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        var repository = options.Repository.Trim().Trim('/');
        if (repository.Length == 0)
        {
            throw new FiveEToolsBuildException(
                "no repository to download from. There is no default, on purpose: pass --repository "
                + "<owner/name>, or set KnowledgeBase__Source__Repository.");
        }

        var release = FiveEToolsRelease.Parse(
            await source.LatestReleaseAsync(repository, cancellationToken).ConfigureAwait(false),
            repository);
        options.OnResolved?.Invoke(release);

        if (Cache.Holds(repository, release.Tag))
        {
            options.OnReused?.Invoke(release);
            return new FiveEToolsDownloadResult(Cache.Checkout, release, Reused: true);
        }

        // Not a top-up. Whatever is there is either another release or an interrupted one, and both
        // answer to the same thing: start from nothing.
        Cache.Clear();
        Directory.CreateDirectory(Cache.Root);

        options.OnDownloading?.Invoke(release);
        await source.DownloadAsync(release.Url, Cache.Archive, cancellationToken).ConfigureAwait(false);

        var files = Extract(Cache.Archive, Cache.Checkout);
        File.Delete(Cache.Archive);
        options.OnExtracted?.Invoke(files);

        if (!Cache.IsComplete())
        {
            throw new FiveEToolsBuildException(
                $"{repository} release {release.Tag} unpacked without a complete data folder "
                + $"({string.Join(" and ", FiveEToolsParser.RequiredIndexes)} are what it needs). "
                + "The archive is not the shape this expects.");
        }

        // Last, so that a stamp means "every file is on disk" and nothing else.
        Cache.Write(new FiveEToolsDownloadStamp
        {
            Repository = repository,
            Tag = release.Tag,
            Asset = release.Asset,
            Files = files,
            CompletedAt = DateTimeOffset.UtcNow,
        });

        return new FiveEToolsDownloadResult(Cache.Checkout, release, Reused: false);
    }

    /// <summary>
    /// The entries worth writing out: the data folder, and the <c>package.json</c> the parse reads
    /// the corpus version from.
    /// </summary>
    private static bool Wanted(string entry) =>
        entry.StartsWith("data/", StringComparison.Ordinal)
        || entry.Equals("package.json", StringComparison.Ordinal);

    /// <summary>
    /// Unpacks the wanted entries, and returns how many files it wrote.
    /// </summary>
    /// <remarks>
    /// Each destination is resolved and checked to be inside the target folder before anything is
    /// written. An archive naming <c>../../.ssh/authorized_keys</c> is the classic way a downloaded
    /// zip writes outside the folder it was meant to, and this is a downloaded zip.
    /// </remarks>
    private static int Extract(string archive, string destination)
    {
        using var zip = ZipFile.OpenRead(archive);

        var root = Path.GetFullPath(destination);
        var inside = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        var written = 0;

        foreach (var entry in zip.Entries)
        {
            if (!Wanted(entry.FullName)) continue;
            if (entry.FullName.EndsWith('/')) continue;

            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var target = Path.GetFullPath(Path.Combine(root, relative));
            if (!target.StartsWith(inside, StringComparison.Ordinal))
            {
                throw new FiveEToolsBuildException(
                    $"the archive holds an entry that would be written outside {destination}: "
                    + $"{entry.FullName}. Unpacking stopped; no stamp is written, so the next run "
                    + "starts from scratch.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
            written++;
        }

        return written;
    }
}
