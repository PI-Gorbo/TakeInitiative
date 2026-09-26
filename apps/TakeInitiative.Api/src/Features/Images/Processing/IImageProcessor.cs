namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// Turns an upload into its two WebP variants. The Discord import (step 24) goes through
/// it too.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Processes the image in <paramref name="input"/>. Throws <see cref="ImageRejectedException"/>
    /// for a type that is not JPEG, PNG, WebP or GIF (415), too many pixels, or bytes that do
    /// not decode (400).
    /// </summary>
    Task<ProcessedImage> Process(Stream input, CancellationToken ct);
}
