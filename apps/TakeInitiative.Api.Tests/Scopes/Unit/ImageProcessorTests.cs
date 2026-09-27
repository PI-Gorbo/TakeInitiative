using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using TakeInitiative.Api.Features.Images;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>Step 16a.4: the type comes from the bytes, limits come before decoding, and no metadata survives.</summary>
public class ImageProcessorTests : IDisposable
{
    private readonly SkiaImageProcessor _processor = new(Options.Create(new ImageOptions()));

    public void Dispose() => _processor.Dispose();

    private Task<ProcessedImage> Process(string fixture)
        => _processor.Process(new MemoryStream(ImageFixtures.Bytes(fixture)), CancellationToken.None);

    private static SKBitmap DecodeWebp(ProcessedVariant variant)
    {
        using var codec = SKCodec.Create(SKData.CreateCopy(variant.Bytes));
        codec.Should().NotBeNull();
        codec!.EncodedFormat.Should().Be(SKEncodedImageFormat.Webp);
        codec.Info.Width.Should().Be(variant.Width);
        codec.Info.Height.Should().Be(variant.Height);
        return SKBitmap.Decode(variant.Bytes);
    }

    [Theory]
    [InlineData(ImageFixtures.RotatedExif6Gps, "jpeg")]
    [InlineData(ImageFixtures.Alpha, "png")]
    [InlineData(ImageFixtures.Webp, "webp")]
    [InlineData(ImageFixtures.AnimatedGif, "gif")]
    [InlineData(ImageFixtures.Wide, "jpeg")]
    public async Task AcceptedTypes_BecomeTwoWebpVariants(string fixture, string format)
    {
        var processed = await Process(fixture);

        processed.SourceFormat.Should().Be(format);
        foreach (var variant in new[] { processed.Display, processed.Thumb })
        {
            Encoding.ASCII.GetString(variant.Bytes, 0, 4).Should().Be("RIFF");
            Encoding.ASCII.GetString(variant.Bytes, 8, 4).Should().Be("WEBP");
            using var _ = DecodeWebp(variant);
        }
    }

    [Theory]
    [InlineData(ImageFixtures.NotAnImage)]
    [InlineData(ImageFixtures.Svg)]
    [InlineData(ImageFixtures.Heic)]
    public async Task OtherTypes_Are415(string fixture)
    {
        var refused = await FluentActions.Awaiting(() => Process(fixture)).Should().ThrowAsync<ImageRejectedException>();
        refused.Which.StatusCode.Should().Be(415);
        refused.Which.Message.Should().Be("This image type is not supported. Try JPEG or PNG.");
    }

    [Fact]
    public async Task ATruncatedFile_Is400()
    {
        var refused = await FluentActions.Awaiting(() => Process(ImageFixtures.Truncated)).Should().ThrowAsync<ImageRejectedException>();
        refused.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ADecompressionBomb_IsRefusedBeforeDecoding()
    {
        var refused = await FluentActions.Awaiting(() => Process(ImageFixtures.Bomb)).Should().ThrowAsync<ImageRejectedException>();

        refused.Which.StatusCode.Should().Be(400);
        refused.Which.Message.Should().Contain("50 megapixels");
        _processor.Decodes.Should().Be(0);
    }

    [Fact]
    public async Task TheExifOrientation_IsApplied_AndAllMetadataIsDropped()
    {
        // Stored 400 x 200 with red on the left; EXIF 6 turns it upright to 200 x 400, red on top.
        var original = ImageFixtures.Bytes(ImageFixtures.RotatedExif6Gps);
        Encoding.Latin1.GetString(original).Should().Contain("FixtureCam", "the fixture carries EXIF");

        var processed = await Process(ImageFixtures.RotatedExif6Gps);

        (processed.Display.Width, processed.Display.Height).Should().Be((200, 400));
        using var bitmap = DecodeWebp(processed.Display);
        var top = bitmap.GetPixel(100, 20);
        var bottom = bitmap.GetPixel(100, 380);
        top.Red.Should().BeGreaterThan(150);
        top.Blue.Should().BeLessThan(100);
        bottom.Blue.Should().BeGreaterThan(150);
        bottom.Red.Should().BeLessThan(100);

        foreach (var variant in new[] { processed.Display, processed.Thumb })
        {
            var text = Encoding.Latin1.GetString(variant.Bytes);
            text.Should().NotContain("EXIF").And.NotContain("Exif").And.NotContain("XMP ");
            text.Should().NotContain("FixtureCam");
        }
    }

    [Fact]
    public async Task Alpha_IsKept()
    {
        var processed = await Process(ImageFixtures.Alpha);

        using var bitmap = DecodeWebp(processed.Display);
        bitmap.GetPixel(8, 32).Alpha.Should().Be(0);
        var right = bitmap.GetPixel(56, 32);
        right.Alpha.Should().Be(255);
        right.Green.Should().BeGreaterThan(150);
    }

    [Fact]
    public async Task AWideImage_IsScaledByItsLongEdge()
    {
        var processed = await Process(ImageFixtures.Wide);

        (processed.Display.Width, processed.Display.Height).Should().Be((3200, 533));
        (processed.Thumb.Width, processed.Thumb.Height).Should().Be((640, 107));
    }

    [Fact]
    public async Task ASmallImage_IsNeverUpscaled()
    {
        var processed = await Process(ImageFixtures.Webp);

        (processed.Display.Width, processed.Display.Height).Should().Be((300, 200));
        (processed.Thumb.Width, processed.Thumb.Height).Should().Be((300, 200));
    }

    [Fact]
    public async Task AGif_KeepsItsFirstFrame()
    {
        var processed = await Process(ImageFixtures.AnimatedGif);

        using var bitmap = DecodeWebp(processed.Display);
        var pixel = bitmap.GetPixel(50, 40);
        pixel.Red.Should().BeGreaterThan(150);
        pixel.Green.Should().BeLessThan(100);
    }
}
