using System.Net.Http.Headers;

using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Cli;

/// <summary>
/// <see cref="IFiveEToolsArchiveSource" /> against a GitHub-shaped releases API.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place in the repository that makes an outbound request for corpus data, and it
/// is pointed at <see cref="FiveEToolsDownloadOptions.Repository" /> — which has no committed
/// default. The class knows how to fetch *a* release archive; it does not know whose.
/// </para>
/// <para>
/// The JSON is handed back as text rather than read here, so the one fragile part — what a release
/// document looks like — is parsed by <see cref="FiveEToolsRelease.Parse" /> under test, and this
/// class holds nothing but the two calls and their headers.
/// </para>
/// </remarks>
public sealed class GitHubArchiveSource : IFiveEToolsArchiveSource, IDisposable
{
    /// <summary>The forge this talks to unless configured otherwise.</summary>
    public const string DefaultApiBaseUrl = "https://api.github.com";

    /// <summary>
    /// GitHub rejects a request with no user agent, and an honest one is the right thing to send to
    /// somebody else's server anyway.
    /// </summary>
    private const string UserAgent = "TakeInitiative.KnowledgeBase.Cli";

    private readonly HttpClient client;
    private readonly string apiBaseUrl;

    /// <param name="apiBaseUrl">The API root, or null for <see cref="DefaultApiBaseUrl" />.</param>
    /// <param name="client">An injected client, for a test. One is created when null.</param>
    public GitHubArchiveSource(string? apiBaseUrl = null, HttpClient? client = null)
    {
        this.apiBaseUrl = (apiBaseUrl is { Length: > 0 } given ? given : DefaultApiBaseUrl).TrimEnd('/');

        // A download is tens of megabytes over a link this cannot predict, so the timeout is the
        // whole operation's and generous. The default 100 seconds fails a slow connection halfway.
        this.client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        this.client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    /// <inheritdoc />
    public async Task<string> LatestReleaseAsync(string repository, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"{apiBaseUrl}/repos/{repository}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FiveEToolsBuildException(
                $"{repository} has no releases, or does not exist. Check --repository <owner/name>.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DownloadAsync(Uri url, string destination, CancellationToken cancellationToken)
    {
        using var response = await client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Streamed rather than buffered: the archive is tens of megabytes and there is no reason for
        // any of it to be in memory at once.
        await using var file = File.Create(destination);
        await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => client.Dispose();
}
