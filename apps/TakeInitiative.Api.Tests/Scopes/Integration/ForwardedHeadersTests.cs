using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TakeInitiative.Api.Bootstrap;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>
/// <c>UseForwardedHeaders</c> (29b). A reverse proxy terminates TLS and forwards plain HTTP from a
/// Docker network, so without this middleware every request looks like <c>http</c> coming from the
/// proxy's own address. <c>Program.cs</c> runs it first, before anything can read
/// <c>Request.Scheme</c> or the client address.
/// <para>
/// The interesting half is not the middleware, which is Microsoft's, but the options
/// <see cref="Bootstrap.AddForwardedHeaders"/> gives it — specifically the two cleared lists, which
/// are what makes the headers honoured from an address nobody can predict.
/// <see cref="TheDefaultKnownProxies_IgnoreAProxyOnADockerNetwork"/> is the contrast that justifies
/// clearing them: the same request, the same flags, one list left at its default, and both headers
/// are silently dropped.
/// </para>
/// </summary>
public class ForwardedHeadersTests
{
    /// <summary>An address in Docker's default bridge-network range, which is not loopback.</summary>
    private static readonly IPAddress Proxy = IPAddress.Parse("172.18.0.4");
    private static readonly IPAddress Client = IPAddress.Parse("203.0.113.9");

    [Fact]
    public void TheOptions_ReadOnlyTheTwoHeadersTheProxySets()
    {
        var options = Options();

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        // Not XForwardedHost: AllowedHosts names the API's real host, and honouring a forwarded host
        // would let the header choose the host instead.
        options.ForwardedHeaders.Should().NotHaveFlag(ForwardedHeaders.XForwardedHost);
    }

    [Fact]
    public void TheOptions_TrustAProxyAtAnyAddress()
    {
        var options = Options();

        // Empty on purpose. The proxy reaches the API across a Docker network whose address is
        // assigned when the network is created, so there is no address to list; emptying both lists
        // is what turns the check off. Safe only because nothing publishes the API's port — see
        // Bootstrap.AddForwardedHeaders.
        options.KnownIPNetworks.Should().BeEmpty();
        options.KnownProxies.Should().BeEmpty();
    }

    [Fact]
    public async Task AProxiedRequest_IsSeenAsHttpsFromTheRealClient()
    {
        var context = Proxied();

        await Run(Options(), context);

        context.Request.Scheme.Should().Be("https");
        context.Connection.RemoteIpAddress.Should().Be(Client);
    }

    [Fact]
    public async Task AnUnproxiedRequest_IsLeftAlone()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = Proxy;

        await Run(Options(), context);

        // Dev and the tests send no X-Forwarded-* at all, so the middleware is a no-op there.
        context.Request.Scheme.Should().Be("http");
        context.Connection.RemoteIpAddress.Should().Be(Proxy);
    }

    [Fact]
    public async Task TheDefaultKnownProxies_IgnoreAProxyOnADockerNetwork()
    {
        var context = Proxied();

        // The same flags, but KnownNetworks/KnownProxies left at their defaults, which are loopback
        // alone. This is what the API would do if the two Clear() calls were dropped.
        await Run(
            new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            },
            context);

        context.Request.Scheme.Should().Be("http", "the proxy is not on the default known list");
        context.Connection.RemoteIpAddress.Should().Be(Proxy);
    }

    private static DefaultHttpContext Proxied()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = Proxy;
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = Client.ToString();
        return context;
    }

    private static Task Run(ForwardedHeadersOptions options, HttpContext context)
    {
        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Microsoft.Extensions.Options.Options.Create(options));
        return middleware.Invoke(context);
    }

    /// <summary>The options the API actually runs with, read back out of its own registration.</summary>
    private static ForwardedHeadersOptions Options()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddForwardedHeaders();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}
