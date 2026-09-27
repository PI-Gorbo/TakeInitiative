using Marten.Metadata;

namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// An uploaded picture (glossary: Image), stored as two WebP variants. A Marten document,
/// not an event stream: it is storage bookkeeping (where the bytes are, who uploaded them,
/// whether they are on a note). The domain fact "this note has these images" goes on the
/// note's events (16b). Optimistic concurrency (<see cref="Version"/>) turns two writers
/// racing on one image into a conflict for the loser.
/// </summary>
public class Image : IVersioned
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid UploaderMemberId { get; set; }
    /// <summary>Kept at Postgres's precision (microseconds).</summary>
    public DateTimeOffset UploadedAt { get; set; }
    /// <summary>Of the display variant.</summary>
    public int Width { get; set; }
    /// <summary>Of the display variant.</summary>
    public int Height { get; set; }
    public required ImageVariants Variants { get; set; }

    /// <summary>The note the image is on (16b). Null until its note is posted.</summary>
    public Guid? NoteId { get; set; }
    public DateTimeOffset? AttachedAt { get; set; }

    /// <summary>
    /// Set when the image is deleted. Its blobs are still to delete: the sweeper deletes
    /// them and then this document, and retries when the blob store fails.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public Guid Version { get; set; }

    public ImageVariant? Variant(string name) => name switch
    {
        ImageVariants.DisplayName => Variants.Display,
        ImageVariants.ThumbName => Variants.Thumb,
        _ => null,
    };

    /// <summary><c>campaigns/{campaignId}/images/{imageId}/{variant}.webp</c>.</summary>
    public static string BlobKey(Guid campaignId, Guid imageId, string variant)
        => $"campaigns/{campaignId}/images/{imageId}/{variant}.webp";
}

public record ImageVariants
{
    public const string DisplayName = "display";
    public const string ThumbName = "thumb";

    /// <summary>For the full-screen viewer: long edge at most 3200 px.</summary>
    public required ImageVariant Display { get; init; }
    /// <summary>For the stream, galleries and the composer: long edge at most 640 px.</summary>
    public required ImageVariant Thumb { get; init; }

    public IEnumerable<ImageVariant> All() => [Display, Thumb];
}
