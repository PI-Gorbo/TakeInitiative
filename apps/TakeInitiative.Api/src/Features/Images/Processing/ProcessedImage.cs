namespace TakeInitiative.Api.Features.Images;

/// <summary>The two WebP variants of an upload, with no metadata. The original is never kept.</summary>
public record ProcessedImage(ProcessedVariant Display, ProcessedVariant Thumb)
{
    /// <summary>The format the upload was in, read from its bytes.</summary>
    public required string SourceFormat { get; init; }
}

public record ProcessedVariant(byte[] Bytes, int Width, int Height)
{
    public const string ContentType = "image/webp";
}

/// <summary>An upload the processor refuses, with the status and message the API answers.</summary>
public class ImageRejectedException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public const string UnsupportedMessage = "This image type is not supported. Try JPEG or PNG.";
    public const string TooManyPixelsMessage = "This image is too large. Images can be at most 50 megapixels.";
    public const string UnreadableMessage = "This image could not be read. It may be damaged.";

    public static ImageRejectedException Unsupported() => new(StatusCodes.Status415UnsupportedMediaType, UnsupportedMessage);
    public static ImageRejectedException TooManyPixels() => new(StatusCodes.Status400BadRequest, TooManyPixelsMessage);
    public static ImageRejectedException Unreadable() => new(StatusCodes.Status400BadRequest, UnreadableMessage);
}
