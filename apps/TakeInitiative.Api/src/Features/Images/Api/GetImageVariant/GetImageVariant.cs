using FastEndpoints;
using Marten;
using Microsoft.Net.Http.Headers;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Images;

public record GetImageVariantRequest
{
    public Guid CampaignId { get; init; }
    public Guid ImageId { get; init; }
    /// <summary><c>display</c> or <c>thumb</c>. Anything else is a 404.</summary>
    public string Variant { get; init; } = "";
}

/// <summary>
/// Streams one variant of an image from the blob store, after the image's read rule
/// (<see cref="ImageAccess"/>) on every request. The bucket is never exposed and there are
/// no presigned URLs: a URL is only a name, and every fetch is checked (invariant 5).
/// <para>
/// The browser may keep the bytes (<c>private, no-cache</c> with a strong ETag), but asks
/// every time, and "not modified" is only answered after the check passes. So a revoked
/// image stops being served at once, and a repeat view costs a small request.
/// </para>
/// </summary>
public class GetImageVariant(IDocumentSession session, IBlobStore blobs, ILogger<GetImageVariant> logger)
    : Endpoint<GetImageVariantRequest>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/images/{ImageId}/{Variant}");
        Description(b => b
            .ClearDefaultProduces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status200OK, typeof(IFormFile), ProcessedVariant.ContentType)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblemFE(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(GetImageVariantRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var image = await this.RequireVisibleImage(session, req.CampaignId, req.ImageId, member, ct);
        var variant = image.Variant(req.Variant)
            ?? throw NotFound();

        // Variants never change, so the ETag only names the variant.
        var etag = $"\"{image.Id}-{req.Variant}\"";
        var headers = HttpContext.Response.Headers;
        headers.ETag = etag;
        headers.CacheControl = "private, no-cache";

        // Only now, after the visibility check, may the answer be "not modified".
        if (Matches(HttpContext.Request.Headers.IfNoneMatch, etag))
        {
            await SendResultAsync(Results.StatusCode(StatusCodes.Status304NotModified));
            return;
        }

        await using var blob = await blobs.GetAsync(variant.Key, ct);
        if (blob is null)
        {
            logger.LogError("Image {ImageId} has no {Variant} blob at {Key}", image.Id, req.Variant, variant.Key);
            throw NotFound();
        }

        headers.XContentTypeOptions = "nosniff";
        headers.ContentDisposition = "inline";
        await SendStreamAsync(blob.Content, fileLengthBytes: blob.Length, contentType: variant.ContentType, cancellation: ct);
    }

    private Exception NotFound()
    {
        ThrowError(ImageAccess.NotFoundMessage, StatusCodes.Status404NotFound);
        return null!;
    }

    /// <summary>Whether an <c>If-None-Match</c> header names this ETag (or is <c>*</c>).</summary>
    internal static bool Matches(Microsoft.Extensions.Primitives.StringValues ifNoneMatch, string etag)
    {
        foreach (var header in ifNoneMatch)
        {
            if (header is null) continue;
            foreach (var raw in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = raw.StartsWith("W/", StringComparison.Ordinal) ? raw[2..] : raw;
                if (candidate == "*" || candidate == etag)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
