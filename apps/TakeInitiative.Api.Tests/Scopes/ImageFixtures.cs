namespace TakeInitiative.Api.Tests;

/// <summary>The files in <c>Fixtures/images</c>, made by <c>Fixtures/images/generate.py</c>.</summary>
public static class ImageFixtures
{
    public const string RotatedExif6Gps = "rotated-exif6-gps.jpg";
    public const string Alpha = "alpha.png";
    public const string Webp = "photo.webp";
    public const string AnimatedGif = "animated.gif";
    public const string Wide = "wide-6000x1000.jpg";
    public const string Truncated = "truncated.jpg";
    public const string NotAnImage = "not-an-image.jpg";
    public const string Svg = "drawing.svg";
    public const string Heic = "photo.heic";
    public const string Bomb = "bomb-20000.png";

    public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "images", name);

    public static byte[] Bytes(string name) => File.ReadAllBytes(PathOf(name));
}
