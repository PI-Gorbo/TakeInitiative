using System.Collections.Concurrent;
using TakeInitiative.Api.Features.Users;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>Auth.samstack.org, played by the test: each session cookie answers what the test set.</summary>
public class FakeSamstackSessions : ISamstackSessions
{
    public ConcurrentDictionary<string, SamstackSession> Sessions { get; } = new();

    public Task<SamstackSession> Get(string sessionCookie, CancellationToken ct)
        => Sessions.TryGetValue(sessionCookie, out var session)
            ? Task.FromResult(session)
            : throw new HttpRequestException("auth.samstack.org is down");

    /// <summary>A session cookie that auth says is <paramref name="user"/>'s, as <c>name=value</c>.</summary>
    public string SignIn(string token, SamstackUser user)
    {
        var cookie = $"__Secure-samstack.session_token={token}";
        Sessions[cookie] = new SamstackSession.SignedIn(user);
        return cookie;
    }
}
