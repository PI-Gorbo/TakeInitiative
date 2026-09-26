using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Images;

public record PostImageRequest
{
    public Guid CampaignId { get; init; }
    /// <summary>The image: JPEG, PNG, WebP or GIF, at most 20 MB. Read as a stream, never bound.</summary>
    public IFormFile? File { get; init; }
}

public record ImageResponse
{
    public required Guid Id { get; init; }
    /// <summary>Of the display variant, so a note can be laid out before any byte arrives.</summary>
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }

    public static ImageResponse From(Image image) => new()
    {
        Id = image.Id,
        Width = image.Width,
        Height = image.Height,
        UploadedAt = image.UploadedAt,
    };
}

/// <summary>
/// A member uploads one image (<c>multipart/form-data</c>, field <c>file</c>). It is stored
/// as two WebP variants and is visible to its uploader only until its note is posted.
/// </summary>
public class PostImage(
    IDocumentSession session,
    IBlobStore blobs,
    IImageProcessor processor,
    IOptions<ImageOptions> options,
    [FromKeyedServices(SessionGap.ClockKey)] TimeProvider clock,
    ILogger<PostImage> logger) : Endpoint<PostImageRequest, ImageResponse>
{
    public const string FileField = "file";
    public const string TooManyUnpostedMessage = "Post or remove some images first.";
    public const string TooLargeMessage = "This image is too large. Images can be at most 20 MB.";
    public const string NoFileMessage = "Send one image in the 'file' field.";

    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/images");
        // The body is streamed by hand (FormFileSectionsAsync), so nothing is buffered
        // before the caller's membership and the size cap are checked.
        AllowFileUploads(dontAutoBindFormData: true);
    }

    public override async Task HandleAsync(PostImageRequest req, CancellationToken ct)
    {
        var maxFile = options.Value.MaxUploadBytes;
        var maxBody = maxFile + ImageOptions.MultipartOverheadBytes;
        // Kestrel stops reading past this, so a bigger body is refused without being buffered.
        var sizeFeature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = maxBody;
        }

        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        if (HttpContext.Request.ContentLength > maxBody)
        {
            ThrowError(TooLargeMessage, StatusCodes.Status413PayloadTooLarge);
        }

        var unposted = await session.Query<Image>()
            .Where(i => i.CampaignId == req.CampaignId && i.UploaderMemberId == member.MemberId
                && i.NoteId == null && i.DeletedAt == null)
            .CountAsync(ct);
        if (unposted >= options.Value.MaxUnposted)
        {
            ThrowError(TooManyUnpostedMessage, StatusCodes.Status409Conflict);
        }

        await using var upload = await ReadFile(maxFile, ct);

        ProcessedImage processed;
        try
        {
            processed = await processor.Process(upload, ct);
        }
        catch (ImageRejectedException e)
        {
            ThrowError(e.Message, e.StatusCode);
            return;
        }

        var id = Guid.NewGuid();
        var image = new Image
        {
            Id = id,
            CampaignId = req.CampaignId,
            UploaderMemberId = member.MemberId,
            UploadedAt = Microseconds.Truncate(clock.GetUtcNow()),
            Width = processed.Display.Width,
            Height = processed.Display.Height,
            Variants = new ImageVariants
            {
                Display = Variant(req.CampaignId, id, ImageVariants.DisplayName, processed.Display),
                Thumb = Variant(req.CampaignId, id, ImageVariants.ThumbName, processed.Thumb),
            },
        };

        try
        {
            await Put(image.Variants.Display, processed.Display, ct);
            await Put(image.Variants.Thumb, processed.Thumb, ct);
            session.Store(image);
            await session.SaveChangesAsync(ct);
        }
        catch
        {
            // Nothing points at the blobs yet: remove them, as far as the store lets us.
            foreach (var variant in image.Variants.All())
            {
                try { await blobs.DeleteAsync(variant.Key, CancellationToken.None); }
                catch (Exception e) { logger.LogWarning(e, "Could not remove blob {Key} after a failed upload", variant.Key); }
            }
            throw;
        }

        await SendAsync(ImageResponse.From(image), cancellation: ct);
    }

    /// <summary>The <c>file</c> part, copied into memory up to <paramref name="maxFile"/> bytes.</summary>
    private async Task<MemoryStream> ReadFile(long maxFile, CancellationToken ct)
    {
        await foreach (var section in FormFileSectionsAsync(ct))
        {
            if (section is null || section.Name != FileField)
            {
                continue;
            }
            var copy = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await section.Section.Body.ReadAsync(buffer, ct)) > 0)
            {
                if (copy.Length + read > maxFile)
                {
                    await copy.DisposeAsync();
                    ThrowError(TooLargeMessage, StatusCodes.Status413PayloadTooLarge);
                }
                copy.Write(buffer, 0, read);
            }
            copy.Position = 0;
            return copy;
        }
        ThrowError(NoFileMessage, StatusCodes.Status400BadRequest);
        return null!;
    }

    private static ImageVariant Variant(Guid campaignId, Guid imageId, string name, ProcessedVariant processed) => new()
    {
        Key = Image.BlobKey(campaignId, imageId, name),
        ContentType = ProcessedVariant.ContentType,
        Length = processed.Bytes.LongLength,
        Width = processed.Width,
        Height = processed.Height,
    };

    private async Task Put(ImageVariant variant, ProcessedVariant processed, CancellationToken ct)
    {
        using var content = new MemoryStream(processed.Bytes, writable: false);
        await blobs.PutAsync(variant.Key, content, variant.ContentType, ct);
    }
}
