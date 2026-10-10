using System.IO.Compression;

using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// <c>--download</c> (2026-10-10): reading a release document, recognising a corpus already on
/// disk, and unpacking one that is not.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here touches the network. <see cref="FiveEToolsDownload" /> takes an
/// <see cref="IFiveEToolsArchiveSource" /> precisely so that the two things worth pinning — what a
/// release document looks like, and what is done with the archive it names — are testable without
/// one, and so that the forge's JSON shape is asserted here rather than discovered in production.
/// </para>
/// <para>
/// The behaviour these protect is <b>idempotence</b>. A second run against the same release must
/// fetch nothing at all, because the alternative is re-downloading tens of megabytes of somebody
/// else's data every time an operator repeats a command.
/// </para>
/// </remarks>
public class DownloadTests
{
    /// <summary>A release document shaped like GitHub's, with the given assets.</summary>
    private static string ReleaseJson(string tag, string assets) =>
        $$"""{ "tag_name": "{{tag}}", "assets": [{{assets}}] }""";

    private static string Asset(string name, string url, long? size = 48_800_000) =>
        $$"""{ "name": "{{name}}", "browser_download_url": "{{url}}"{{(size is { } bytes ? $", \"size\": {bytes}" : "")}} }""";

    // --- the release document -----------------------------------------------------------------

    /// <summary>The tag and the zip asset, which is what a download needs and all it needs.</summary>
    [Fact]
    public void A_release_yields_its_tag_and_its_zip_asset()
    {
        var release = FiveEToolsRelease.Parse(
            ReleaseJson("v2.37.0", Asset("5etools-v2.37.0.zip", "https://example.test/a.zip")),
            "owner/name");

        Assert.Equal("v2.37.0", release.Tag);
        Assert.Equal("5etools-v2.37.0.zip", release.Asset);
        Assert.Equal("https://example.test/a.zip", release.Url.ToString());
        Assert.Equal(48_800_000, release.Bytes);
    }

    /// <summary>
    /// A release carries more than one asset — checksums, signatures, image archives. The zip is
    /// the one that is a corpus.
    /// </summary>
    [Fact]
    public void Assets_that_are_not_zips_are_passed_over()
    {
        var release = FiveEToolsRelease.Parse(
            ReleaseJson(
                "v1.0.0",
                string.Join(
                    ",",
                    Asset("notes.txt", "https://example.test/notes.txt"),
                    Asset("sha256sums", "https://example.test/sums"),
                    Asset("site.zip", "https://example.test/site.zip"))),
            "owner/name");

        Assert.Equal("site.zip", release.Asset);
    }

    /// <summary>
    /// The asset URL is the one value in a download that comes from outside and is then acted on,
    /// so plain http is not followed.
    /// </summary>
    [Fact]
    public void An_asset_served_over_http_is_not_a_download()
    {
        var error = Assert.Throws<FiveEToolsBuildException>(() => FiveEToolsRelease.Parse(
            ReleaseJson("v1.0.0", Asset("site.zip", "http://example.test/site.zip")),
            "owner/name"));

        Assert.Contains("no .zip asset to download over https", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A repository with no releases answers without a tag, and that is not a corpus.</summary>
    [Fact]
    public void An_answer_with_no_tag_names_the_repository()
    {
        var error = Assert.Throws<FiveEToolsBuildException>(
            () => FiveEToolsRelease.Parse("""{ "message": "Not Found" }""", "owner/name"));

        Assert.Contains("owner/name has no latest release", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Something that is not JSON at all, which is what a proxy error page looks like.</summary>
    [Fact]
    public void An_answer_that_is_not_json_is_reported_as_such()
    {
        var error = Assert.Throws<FiveEToolsBuildException>(
            () => FiveEToolsRelease.Parse("<html>502 Bad Gateway</html>", "owner/name"));

        Assert.Contains("did not answer with a release", error.Message, StringComparison.Ordinal);
    }

    // --- the cache ----------------------------------------------------------------------------

    /// <summary>An empty cache holds nothing, and says so without throwing.</summary>
    [Fact]
    public void An_empty_cache_holds_nothing()
    {
        using var cache = new TemporaryCache();

        Assert.False(cache.Value.Holds("owner/name", "v1.0.0"));
        Assert.Null(cache.Value.Read());
    }

    /// <summary>
    /// A stamp for another release is not a hit. This is the whole mechanism: upstream cutting
    /// v2.38.0 has to invalidate a v2.37.0 cache.
    /// </summary>
    [Fact]
    public void A_cache_of_another_release_is_not_a_hit()
    {
        using var cache = new TemporaryCache();
        Unpack(cache, Archive(), "owner/name", "v1.0.0");

        Assert.True(cache.Value.Holds("owner/name", "v1.0.0"));
        Assert.False(cache.Value.Holds("owner/name", "v1.0.1"));
    }

    /// <summary>
    /// And neither is a stamp from another repository, so two corpora cannot be read as each
    /// other through one cache path.
    /// </summary>
    [Fact]
    public void A_cache_of_another_repository_is_not_a_hit()
    {
        using var cache = new TemporaryCache();
        Unpack(cache, Archive(), "owner/name", "v1.0.0");

        Assert.False(cache.Value.Holds("someone/else", "v1.0.0"));
    }

    /// <summary>
    /// A stamp with the corpus deleted from under it is not a hit either. The stamp says a
    /// download finished; it cannot say the files are still there.
    /// </summary>
    [Fact]
    public void A_stamp_without_its_corpus_is_not_a_hit()
    {
        using var cache = new TemporaryCache();
        Unpack(cache, Archive(), "owner/name", "v1.0.0");

        Directory.Delete(Path.Combine(cache.Value.Checkout, "data", "spells"), recursive: true);

        Assert.False(cache.Value.Holds("owner/name", "v1.0.0"));
    }

    // --- the download -------------------------------------------------------------------------

    /// <summary>
    /// The data folder and the <c>package.json</c> come out; the site does not. The archive holds
    /// a whole website and the ingest reads none of it.
    /// </summary>
    [Fact]
    public async Task Only_the_data_and_the_package_manifest_are_unpacked()
    {
        using var cache = new TemporaryCache();
        var archives = new FakeArchives(
            ReleaseJson("v2.37.0", Asset("5etools-v2.37.0.zip", "https://example.test/a.zip")),
            Archive());

        var result = await new FiveEToolsDownload(archives, cache.Value).RunAsync(Options());

        Assert.False(result.Reused);
        Assert.Equal(cache.Value.Checkout, result.Checkout);
        Assert.True(File.Exists(Path.Combine(result.Checkout, "data", "bestiary", "index.json")));
        Assert.True(File.Exists(Path.Combine(result.Checkout, "package.json")));
        Assert.False(Directory.Exists(Path.Combine(result.Checkout, "js")));
        Assert.False(File.Exists(Path.Combine(result.Checkout, "5etools.html")));
    }

    /// <summary>The archive itself is not left behind once it has been unpacked.</summary>
    [Fact]
    public async Task The_archive_is_deleted_once_it_is_unpacked()
    {
        using var cache = new TemporaryCache();
        var archives = new FakeArchives(
            ReleaseJson("v2.37.0", Asset("5etools-v2.37.0.zip", "https://example.test/a.zip")),
            Archive());

        await new FiveEToolsDownload(archives, cache.Value).RunAsync(Options());

        Assert.False(File.Exists(cache.Value.Archive));
    }

    /// <summary>
    /// The point of the whole mechanism: running it twice fetches once. The release is still
    /// looked up — that is how "latest" is known — but no bytes move.
    /// </summary>
    [Fact]
    public async Task A_second_run_of_the_same_release_downloads_nothing()
    {
        using var cache = new TemporaryCache();
        var archives = new FakeArchives(
            ReleaseJson("v2.37.0", Asset("5etools-v2.37.0.zip", "https://example.test/a.zip")),
            Archive());
        var download = new FiveEToolsDownload(archives, cache.Value);

        await download.RunAsync(Options());
        var second = await download.RunAsync(Options());

        Assert.True(second.Reused);
        Assert.Equal(1, archives.Downloads);
        Assert.Equal(2, archives.Lookups);
    }

    /// <summary>A new release upstream replaces the cache rather than merging into it.</summary>
    [Fact]
    public async Task A_new_release_upstream_is_fetched_again()
    {
        using var cache = new TemporaryCache();
        var archives = new FakeArchives(
            ReleaseJson("v2.37.0", Asset("5etools-v2.37.0.zip", "https://example.test/a.zip")),
            Archive());
        var download = new FiveEToolsDownload(archives, cache.Value);

        await download.RunAsync(Options());

        archives.Json = ReleaseJson("v2.38.0", Asset("5etools-v2.38.0.zip", "https://example.test/b.zip"));
        var second = await download.RunAsync(Options());

        Assert.False(second.Reused);
        Assert.Equal(2, archives.Downloads);
        Assert.Equal("v2.38.0", cache.Value.Read()?.Tag);
    }

    /// <summary>
    /// An entry that would be written outside the cache is refused. This is a zip from the
    /// internet, and that is the classic way one writes somewhere it was not invited.
    /// </summary>
    [Fact]
    public async Task An_entry_that_escapes_the_cache_is_refused()
    {
        using var cache = new TemporaryCache();
        var archives = new FakeArchives(
            ReleaseJson("v1.0.0", Asset("x.zip", "https://example.test/x.zip")),
            Archive(escaping: true));

        var error = await Assert.ThrowsAsync<FiveEToolsBuildException>(
            () => new FiveEToolsDownload(archives, cache.Value).RunAsync(Options()));

        Assert.Contains("written outside", error.Message, StringComparison.Ordinal);
        Assert.Null(cache.Value.Read());
    }

    /// <summary>
    /// An archive that unpacks without the two required indexes is a failure at the download, not
    /// a partial-folder report three steps later.
    /// </summary>
    [Fact]
    public async Task An_archive_without_a_data_folder_fails_the_download()
    {
        using var cache = new TemporaryCache();
        var archives = new FakeArchives(
            ReleaseJson("v1.0.0", Asset("x.zip", "https://example.test/x.zip")),
            Archive(data: false));

        var error = await Assert.ThrowsAsync<FiveEToolsBuildException>(
            () => new FiveEToolsDownload(archives, cache.Value).RunAsync(Options()));

        Assert.Contains("without a complete data folder", error.Message, StringComparison.Ordinal);
        Assert.Null(cache.Value.Read());
    }

    /// <summary>
    /// No repository, no download — and the message says there is no default on purpose, because
    /// an operator reading it is about to go looking for the one they assume exists.
    /// </summary>
    [Fact]
    public async Task No_repository_is_refused_and_says_there_is_no_default()
    {
        using var cache = new TemporaryCache();
        var archives = new FakeArchives("{}", Archive());

        var error = await Assert.ThrowsAsync<FiveEToolsBuildException>(
            () => new FiveEToolsDownload(archives, cache.Value)
                .RunAsync(new FiveEToolsDownloadOptions { Repository = "  " }));

        Assert.Contains("no repository to download from", error.Message, StringComparison.Ordinal);
        Assert.Contains("no default, on purpose", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, archives.Lookups);
    }

    /// <summary>Clearing it takes the corpus, the stamp and the folder.</summary>
    [Fact]
    public async Task Clearing_the_cache_removes_everything()
    {
        using var cache = new TemporaryCache();
        var archives = new FakeArchives(
            ReleaseJson("v1.0.0", Asset("x.zip", "https://example.test/x.zip")),
            Archive());

        await new FiveEToolsDownload(archives, cache.Value).RunAsync(Options());
        cache.Value.Clear();

        Assert.False(Directory.Exists(cache.Value.Root));
        Assert.Null(cache.Value.Read());
    }

    // --- helpers ------------------------------------------------------------------------------

    private static FiveEToolsDownloadOptions Options() => new() { Repository = "owner/name" };

    /// <summary>
    /// A zip shaped like a 5eTools release: a data folder, a manifest, and the site files that
    /// should not be unpacked.
    /// </summary>
    private static string Archive(bool data = true, bool escaping = false)
    {
        var file = Path.Combine(Path.GetTempPath(), $"ti-5etools-{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(file, ZipArchiveMode.Create);

        Add(zip, "package.json", """{ "version": "2.37.0" }""");
        Add(zip, "5etools.html", "<html></html>");
        Add(zip, "js/render.js", "export const render = () => {};");

        if (data)
        {
            Add(zip, "data/bestiary/index.json", """{ "TST": "bestiary-tst.json" }""");
            Add(zip, "data/bestiary/bestiary-tst.json", """{ "monster": [] }""");
            Add(zip, "data/spells/index.json", "{}");
        }

        if (escaping) Add(zip, "data/../../escaped.json", "{}");

        return file;
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        using var stream = new StreamWriter(zip.CreateEntry(name).Open());
        stream.Write(content);
    }

    /// <summary>Unpacks an archive into a cache the way a real run would, stamp and all.</summary>
    private static void Unpack(TemporaryCache cache, string archive, string repository, string tag)
    {
        var archives = new FakeArchives(
            ReleaseJson(tag, Asset($"{tag}.zip", "https://example.test/a.zip")),
            archive);

        new FiveEToolsDownload(archives, cache.Value)
            .RunAsync(new FiveEToolsDownloadOptions { Repository = repository })
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>A cache in a throwaway folder, so a test never touches the real one.</summary>
    private sealed class TemporaryCache : IDisposable
    {
        public TemporaryCache() => Value = new FiveEToolsDownloadCache(
            Path.Combine(Path.GetTempPath(), $"ti-5etools-cache-{Guid.NewGuid():N}"));

        public FiveEToolsDownloadCache Value { get; }

        public void Dispose() => Value.Clear();
    }

    /// <summary>A forge that answers from memory and copies a local file in place of a fetch.</summary>
    private sealed class FakeArchives(string json, string archive) : IFiveEToolsArchiveSource
    {
        public string Json { get; set; } = json;

        public int Lookups { get; private set; }

        public int Downloads { get; private set; }

        public Task<string> LatestReleaseAsync(string repository, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult(Json);
        }

        public Task DownloadAsync(Uri url, string destination, CancellationToken cancellationToken)
        {
            Downloads++;
            File.Copy(archive, destination, overwrite: true);
            return Task.CompletedTask;
        }
    }
}
