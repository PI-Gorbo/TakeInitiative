using System.Text.Json;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// A published release of a 5eTools source repository: a version, and the archive to fetch for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The version is the whole point.</b> A release tag never moves, so it answers "is the copy in
/// the cache the copy upstream has?" with a string comparison and no download. That is what makes
/// <see cref="FiveEToolsDownload" /> idempotent, and it is why this reads a release rather than a
/// branch: a branch head is also an identifier, but it is one that requires the git history to
/// obtain, and the release carries a ready-made archive instead.
/// </para>
/// <para>
/// <see cref="Parse" /> takes the JSON rather than fetching it, so the shape of a forge's API is
/// pinned by a test and the HTTP lives in the CLI. See <see cref="IFiveEToolsArchiveSource" />.
/// </para>
/// </remarks>
public sealed record FiveEToolsRelease
{
    /// <summary>The release's immutable tag, as upstream named it — <c>v2.37.0</c>.</summary>
    public required string Tag { get; init; }

    /// <summary>The archive's file name, which is recorded so a stamp says what it holds.</summary>
    public required string Asset { get; init; }

    /// <summary>Where to fetch the archive from.</summary>
    public required Uri Url { get; init; }

    /// <summary>What the archive should weigh, if the release says. Reported, never enforced.</summary>
    public long? Bytes { get; init; }

    /// <summary>
    /// Reads a GitHub <c>releases/latest</c> document: its <c>tag_name</c>, and the one asset that
    /// is a zip archive.
    /// </summary>
    /// <remarks>
    /// The <b>asset</b>, not the <c>zipball_url</c> that the same document also carries. They are
    /// different archives: the zipball is a snapshot of the repository, wrapped in a commit-named
    /// folder and holding every file in it, while the published asset is the release upstream
    /// actually built. Preferring the asset keeps this reading what a human following their install
    /// guide would download.
    /// </remarks>
    /// <exception cref="FiveEToolsBuildException">
    /// The document is not a release, or carries no zip asset. Both are reported as a failure rather
    /// than an empty result, because a run that asked to download has nothing else to do.
    /// </exception>
    public static FiveEToolsRelease Parse(string json, string repository)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException failure)
        {
            throw new FiveEToolsBuildException(
                $"{repository} did not answer with a release: {failure.Message}");
        }

        // Disposable here, unlike JsonReader's documents, because nothing this method returns is a
        // window onto it: every field is copied out before the document goes.
        using (document)
        {
            var root = document.RootElement;

            if (Js.AsString(Js.Get(root, "tag_name")) is not { Length: > 0 } tag)
            {
                throw new FiveEToolsBuildException(
                    $"{repository} has no latest release (the answer carried no tag_name).");
            }

            if (Js.Get(root, "assets") is not { } assets || !Js.IsArray(assets))
            {
                throw new FiveEToolsBuildException($"{repository} release {tag} lists no assets.");
            }

            foreach (var asset in assets.EnumerateArray())
            {
                if (Js.AsString(Js.Get(asset, "name")) is not { Length: > 0 } name) continue;
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                if (Js.AsString(Js.Get(asset, "browser_download_url")) is not { Length: > 0 } href) continue;
                if (!Uri.TryCreate(href, UriKind.Absolute, out var url)) continue;

                // https only, and checked here rather than trusted, because this URL is the one
                // thing in a download that comes from outside and gets acted on.
                if (url.Scheme != Uri.UriSchemeHttps) continue;

                return new FiveEToolsRelease
                {
                    Tag = tag,
                    Asset = name,
                    Url = url,
                    Bytes = Js.Get(asset, "size") is { ValueKind: JsonValueKind.Number } size
                        && size.TryGetInt64(out var bytes)
                            ? bytes
                            : null,
                };
            }

            throw new FiveEToolsBuildException(
                $"{repository} release {tag} has no .zip asset to download over https.");
        }
    }
}
