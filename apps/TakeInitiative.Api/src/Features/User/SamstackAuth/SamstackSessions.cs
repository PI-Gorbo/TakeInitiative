using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TakeInitiative.Api.Features.Users;

public class SamstackAuthOptions
{
    public const string SectionKey = "SamstackAuth";

    /// <summary>The auth app. Its session cookie is set on the parent domain, so the API sees it.</summary>
    public required Uri BaseUrl { get; set; }

    /// <summary>This app's id in SamStack's app registry (packages/apps in the SamStack repo).</summary>
    public required string AppId { get; set; }

    /// <summary>
    /// Local development only: every request without a SamStack session is this user, since the
    /// <c>.samstack.org</c> cookie never reaches localhost. Set in launchSettings.json, and dropped
    /// outside Development (<see cref="DevUserFor"/>).
    /// </summary>
    public SamstackDevUser? DevUser { get; set; }

    public static SamstackDevUser? DevUserFor(SamstackDevUser? devUser, bool isDevelopment)
        => isDevelopment ? devUser : null;
}

public class SamstackDevUser
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }

    public SamstackUser ToUser() => new(Id, Email, Name);
}

/// <summary>Who auth.samstack.org says is signed in. Its sessions only ever carry a confirmed email.</summary>
public record SamstackUser(string Id, string Email, string Name);

/// <summary>What <c>GET /api/session</c> answered.</summary>
public abstract record SamstackSession
{
    public sealed record SignedIn(SamstackUser User) : SamstackSession;
    public sealed record SignedOut : SamstackSession;
    /// <summary>Signed in, but this app's access model does not let them in.</summary>
    public sealed record Denied : SamstackSession;
}

public interface ISamstackSessions
{
    /// <param name="sessionCookie">The session cookie alone, as <c>name=value</c>.</param>
    /// <exception cref="HttpRequestException">Auth could not be reached, or answered anything else.</exception>
    Task<SamstackSession> Get(string sessionCookie, CancellationToken ct);
}

/// <summary>
/// Asks auth.samstack.org about a session, the way @thesamstack/nuxt-auth does for the JS apps
/// (SamStack spec §4). A signed-in answer is cached for 60s, keyed on a hash of the cookie so the raw
/// cookie never sits in memory as a key; a sign-out therefore takes up to a minute to reach the API.
/// </summary>
public class SamstackSessions(HttpClient http, IMemoryCache cache, IOptions<SamstackAuthOptions> options) : ISamstackSessions
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

    public async Task<SamstackSession> Get(string sessionCookie, CancellationToken ct)
    {
        var key = $"samstack-session:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionCookie)))}";
        if (cache.TryGetValue(key, out SamstackUser? cached) && cached is not null)
        {
            return new SamstackSession.SignedIn(cached);
        }

        var url = new Uri(options.Value.BaseUrl, $"/api/session?app={Uri.EscapeDataString(options.Value.AppId)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", sessionCookie);
        using var response = await http.SendAsync(request, ct);

        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                var user = ReadUser(await response.Content.ReadAsStringAsync(ct));
                cache.Set(key, user, CacheFor);
                return new SamstackSession.SignedIn(user);
            case HttpStatusCode.Unauthorized:
                return new SamstackSession.SignedOut();
            case HttpStatusCode.Forbidden:
                return new SamstackSession.Denied();
            default:
                throw new HttpRequestException($"auth.samstack.org answered {(int)response.StatusCode} to /api/session.");
        }
    }

    // /api/session is a trust boundary: check the shape rather than trusting the body.
    private static SamstackUser ReadUser(string body)
    {
        using var json = JsonDocument.Parse(body);
        if (json.RootElement.TryGetProperty("user", out var user)
            && user.ValueKind == JsonValueKind.Object
            && Text(user, "id") is { Length: > 0 } id
            && Text(user, "email") is { Length: > 0 } email
            && Text(user, "name") is { } name)
        {
            return new SamstackUser(id, email, name);
        }

        throw new HttpRequestException("auth.samstack.org answered /api/session with a body that has no user.");
    }

    private static string? Text(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
