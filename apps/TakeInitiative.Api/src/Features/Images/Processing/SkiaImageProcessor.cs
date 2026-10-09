using Microsoft.Extensions.Options;
using SkiaSharp;

namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// <see cref="IImageProcessor"/> on SkiaSharp (MIT). The type comes from the bytes, the
/// pixel count is checked from the header before anything is decoded, the EXIF orientation
/// is applied, and the WebP variants carry no metadata at all (phone photos carry GPS
/// positions). A singleton: its semaphore bounds how many images decode at once.
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor, IDisposable
{
    public record VariantSpec(int LongEdge, int Quality);

    public static readonly VariantSpec DisplaySpec = new(3200, 82);
    public static readonly VariantSpec ThumbSpec = new(640, 75);

    private static readonly HashSet<SKEncodedImageFormat> Accepted =
    [
        SKEncodedImageFormat.Jpeg,
        SKEncodedImageFormat.Png,
        SKEncodedImageFormat.Webp,
        SKEncodedImageFormat.Gif,
    ];

    private readonly SemaphoreSlim _gate;
    private readonly long _maxPixels;
    private int _decodes;

    public SkiaImageProcessor(IOptions<ImageOptions> options)
    {
        _gate = new SemaphoreSlim(options.Value.MaxConcurrentProcessing);
        _maxPixels = options.Value.MaxPixels;
    }

    /// <summary>How many images this processor has decoded. Tests read it to prove a refusal came before decoding.</summary>
    internal int Decodes => _decodes;

    public async Task<ProcessedImage> Process(Stream input, CancellationToken ct)
    {
        using var data = await ReadAll(input, ct);
        // SKCodec reads the header only. It returns null for anything Skia cannot read at
        // all: text, SVG (which can carry script), PDF.
        using var codec = SKCodec.Create(data);
        if (codec is null || !Accepted.Contains(codec.EncodedFormat))
        {
            throw ImageRejectedException.Unsupported();
        }

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        if (width <= 0 || height <= 0)
        {
            throw ImageRejectedException.Unreadable();
        }
        // Before any decode: a tiny PNG can claim 20,000 x 20,000 (1.6 GB of pixels).
        if ((long)width * height > _maxPixels)
        {
            throw ImageRejectedException.TooManyPixels();
        }

        await _gate.WaitAsync(ct);
        try
        {
            Interlocked.Increment(ref _decodes);
            using var source = Decode(codec);
            using var image = SKImage.FromBitmap(source);
            var origin = codec.EncodedOrigin;

            return new ProcessedImage(Render(image, origin, DisplaySpec), Render(image, origin, ThumbSpec))
            {
                SourceFormat = codec.EncodedFormat.ToString().ToLowerInvariant(),
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<SKData> ReadAll(Stream input, CancellationToken ct)
    {
        if (input is MemoryStream memory)
        {
            return SKData.CreateCopy(memory.ToArray());
        }
        using var copy = new MemoryStream();
        await input.CopyToAsync(copy, ct);
        return SKData.CreateCopy(copy.ToArray());
    }

    /// <summary>
    /// Decodes the first frame (a GIF keeps only that), converted to sRGB. A big JPEG is
    /// decoded at a reduced scale when that still covers the display variant, which saves
    /// most of the memory a 40-megapixel photo would take.
    /// </summary>
    private static SKBitmap Decode(SKCodec codec)
    {
        var size = new SKSizeI(codec.Info.Width, codec.Info.Height);
        var longEdge = Math.Max(size.Width, size.Height);
        if (longEdge > DisplaySpec.LongEdge)
        {
            var scaled = codec.GetScaledDimensions((float)DisplaySpec.LongEdge / longEdge);
            if (Math.Max(scaled.Width, scaled.Height) >= DisplaySpec.LongEdge)
            {
                size = scaled;
            }
        }

        var alpha = codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul;
        var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, alpha, SKColorSpace.CreateSrgb());
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        // IncompleteInput is a truncated file: Skia fills the rest in grey. Refuse it
        // rather than store half a picture.
        if (result != SKCodecResult.Success)
        {
            bitmap.Dispose();
            throw ImageRejectedException.Unreadable();
        }
        return bitmap;
    }

    /// <summary>One variant: scaled down (never up), turned the right way up, encoded as WebP.</summary>
    private static ProcessedVariant Render(SKImage source, SKEncodedOrigin origin, VariantSpec spec)
    {
        // The long edge does not change with orientation, so size first, then rotate.
        var scale = Math.Min(1.0, (double)spec.LongEdge / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (outWidth, outHeight) = swaps ? (height, width) : (width, height);

        var info = new SKImageInfo(outWidth, outHeight, SKImageInfo.PlatformColorType, source.AlphaType, SKColorSpace.CreateSrgb());
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(OrientationMatrix(origin, width, height));
            // Mipmaps keep a large downscale (6000 px to 640 px) from aliasing.
            canvas.DrawImage(source, SKRect.Create(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        // Skia's encoder writes pixels only: no EXIF, no XMP, no ICC for sRGB.
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Webp, spec.Quality)
            ?? throw new InvalidOperationException("Skia could not encode a WebP image.");
        return new ProcessedVariant(encoded.ToArray(), outWidth, outHeight);
    }

    /// <summary>
    /// Maps an upright-to-be image of <paramref name="w"/> x <paramref name="h"/> (as stored)
    /// onto its displayed orientation. x' = ScaleX x + SkewX y + TransX, y' = SkewY x + ScaleY y + TransY.
    /// </summary>
    internal static SKMatrix OrientationMatrix(SKEncodedOrigin origin, int w, int h) => origin switch
    {
        // Mirrored left to right.
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
        // Upside down.
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
        // Mirrored top to bottom.
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
        // Transposed.
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        // EXIF 6, the usual portrait phone photo: turn 90 degrees clockwise.
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
        // Transverse.
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
        // EXIF 8: turn 90 degrees anticlockwise.
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        _ => SKMatrix.Identity,
    };

    public void Dispose() => _gate.Dispose();
}
