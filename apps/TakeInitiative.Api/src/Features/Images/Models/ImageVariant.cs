namespace TakeInitiative.Api.Features.Images;

/// <summary>One stored rendition of an <see cref="Image"/>. Variants never change once stored.</summary>
public record ImageVariant
{
    public required string Key { get; init; }
    public required string ContentType { get; init; }
    public long Length { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}
