namespace TakeInitiative.Api.Features.Images;

/// <summary>The <c>Images</c> configuration section: the upload limits and the sweeper.</summary>
public class ImageOptions
{
    public const string SectionKey = "Images";

    /// <summary>The largest file one upload may carry.</summary>
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;
    /// <summary>Checked from the header, before anything is decoded (a decompression bomb).</summary>
    public long MaxPixels { get; set; } = 50_000_000;
    /// <summary>How many images on no note one member may hold at once.</summary>
    public int MaxUnposted { get; set; } = 20;
    /// <summary>An image still on no note this long after its upload is deleted.</summary>
    public TimeSpan UnpostedLifetime { get; set; } = TimeSpan.FromHours(24);
    /// <summary>How many images are decoded at once, which bounds memory on a small server.</summary>
    public int MaxConcurrentProcessing { get; set; } = 2;
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(1);
    /// <summary>The first sweep runs this long after start, so a restart still sweeps.</summary>
    public TimeSpan SweepStartDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>The multipart framing around the file, allowed on top of <see cref="MaxUploadBytes"/>.</summary>
    public const long MultipartOverheadBytes = 64 * 1024;
}
