using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TakeInitiative.Api.Features.Users;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>The client for auth.samstack.org's <c>/api/session</c> (SamStack spec §4).</summary>
public class SamstackSessionsTests
{
    private const string Cookie = "__Secure-samstack.session_token=tok.sig%3D";

    private sealed class StubAuth(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static SamstackSessions Client(StubAuth auth) => new(
        new HttpClient(auth),
        new MemoryCache(new MemoryCacheOptions()),
        Options.Create(new SamstackAuthOptions { BaseUrl = new Uri("https://auth.test"), AppId = "takeinitiative" }));

    [Fact]
    public async Task ASignedInSessionIsTheUserAndIsCached()
    {
        var auth = new StubAuth(HttpStatusCode.OK,
            """{"user":{"id":"u1","email":"james@outlook.com","name":"James"},"app":"takeinitiative"}""");
        var client = Client(auth);

        var first = await client.Get(Cookie, default);
        var second = await client.Get(Cookie, default);

        first.Should().Be(new SamstackSession.SignedIn(new SamstackUser("u1", "james@outlook.com", "James")));
        second.Should().Be(first);
        auth.Requests.Should().ContainSingle("the second answer comes from the cache");
        var request = auth.Requests[0];
        request.RequestUri.Should().Be(new Uri("https://auth.test/api/session?app=takeinitiative"));
        request.Headers.GetValues("Cookie").Should().Equal(Cookie);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, typeof(SamstackSession.SignedOut))]
    [InlineData(HttpStatusCode.Forbidden, typeof(SamstackSession.Denied))]
    public async Task ASessionAuthRefusesIsNotCached(HttpStatusCode status, Type expected)
    {
        var auth = new StubAuth(status);
        var client = Client(auth);

        (await client.Get(Cookie, default)).Should().BeOfType(expected);
        (await client.Get(Cookie, default)).Should().BeOfType(expected);
        auth.Requests.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"user":{"id":"u1"}}""")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.BadGateway, "")]
    public async Task AnythingElseIsAnError(HttpStatusCode status, string body)
    {
        var act = () => Client(new StubAuth(status, body)).Get(Cookie, default);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public void TheDevUserIsDroppedOutsideDevelopment()
    {
        var devUser = new SamstackDevUser { Id = "dev-user", Email = "dev@takeinitiative.local", Name = "Dev" };
        SamstackAuthOptions.DevUserFor(devUser, isDevelopment: true).Should().BeSameAs(devUser);
        SamstackAuthOptions.DevUserFor(devUser, isDevelopment: false).Should().BeNull();
    }
}
