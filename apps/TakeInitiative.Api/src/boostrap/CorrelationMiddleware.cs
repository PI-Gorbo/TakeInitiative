using System.Diagnostics;
using Marten;

namespace TakeInitiative.Api.Bootstrap;

/// <summary>
/// Stamps the request-scoped Marten session with a correlation id taken from the
/// request's trace id, so every event appended while handling one request shares it
/// (design §9, invariant 9). The id is echoed back in <c>X-Correlation-Id</c>.
/// </summary>
public class CorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    /// <summary>The event header naming the request (method and path).</summary>
    public const string RequestHeaderKey = "request";

    public async Task InvokeAsync(HttpContext context, IDocumentSession session)
    {
        var correlationId = CorrelationIdFor(context);
        session.CorrelationId = correlationId;
        session.SetHeader(RequestHeaderKey, $"{context.Request.Method} {context.Request.Path}");

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await next(context);
    }

    public static string CorrelationIdFor(HttpContext context)
        => Activity.Current is { } activity && activity.TraceId != default
            ? activity.TraceId.ToHexString()
            : context.TraceIdentifier;
}
