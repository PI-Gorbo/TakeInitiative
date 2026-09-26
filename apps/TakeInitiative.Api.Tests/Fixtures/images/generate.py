# /// script
# requires-python = ">=3.10"
# dependencies = ["pillow==11.3.0", "pillow-heif==1.1.0"]
# ///
"""
Regenerates the image fixtures of step 16a (ImageProcessorTests, ImageUploadTests).

    uv run apps/TakeInitiative.Api.Tests/Fixtures/images/generate.py

Every file is small and deterministic in content, and each one exercises one rule of
SkiaImageProcessor: the type comes from the bytes, the pixel count is checked before
decoding, the EXIF orientation is applied and all metadata is dropped.
"""

import struct
import zlib
from pathlib import Path

from PIL import Image
from pillow_heif import register_heif_opener

HERE = Path(__file__).parent
register_heif_opener()


def halves(size, left, right, mode="RGB"):
    """An image whose left half is one colour and right half another."""
    image = Image.new(mode, size, left)
    image.paste(Image.new(mode, (size[0] // 2, size[1]), right), (size[0] // 2, 0))
    return image


def rotated_exif6_gps():
    # Stored 400 x 200, left red and right blue. EXIF orientation 6 means "turn 90 degrees
    # clockwise to display", so the upright picture is 200 x 400 with red on top.
    image = halves((400, 200), (220, 20, 20), (20, 20, 220))
    exif = Image.Exif()
    exif[0x0112] = 6  # Orientation
    exif[0x010F] = "FixtureCam"  # Make
    gps = {
        1: "N", 2: (51.0, 30.0, 0.0),   # GPSLatitudeRef, GPSLatitude
        3: "W", 4: (0.0, 7.0, 0.0),     # GPSLongitudeRef, GPSLongitude
    }
    exif[0x8825] = gps  # GPSInfo
    image.save(HERE / "rotated-exif6-gps.jpg", quality=80, exif=exif.tobytes())


def alpha_png():
    # 64 x 64: the left half fully transparent, the right half opaque green.
    halves((64, 64), (0, 0, 0, 0), (20, 200, 20, 255), mode="RGBA").save(HERE / "alpha.png")


def photo_webp():
    halves((300, 200), (200, 150, 50), (50, 150, 200)).save(HERE / "photo.webp", quality=80)


def animated_gif():
    # Two frames: red, then green. Only the first is kept.
    frames = [Image.new("RGB", (100, 80), c) for c in ((230, 20, 20), (20, 230, 20))]
    frames[0].save(HERE / "animated.gif", save_all=True, append_images=frames[1:], duration=200, loop=0)


def wide_jpg():
    halves((6000, 1000), 90, 160, mode="L").save(HERE / "wide-6000x1000.jpg", quality=20)


def truncated_jpg():
    buffer = HERE / "truncated.jpg"
    halves((800, 600), (200, 30, 30), (30, 30, 200)).save(buffer, quality=90, progressive=False)
    data = buffer.read_bytes()
    buffer.write_bytes(data[: len(data) // 2])


def not_an_image():
    (HERE / "not-an-image.jpg").write_text("This is a text file with a .jpg name.\n")


def drawing_svg():
    (HERE / "drawing.svg").write_text(
        '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">'
        '<script>alert(1)</script><circle cx="50" cy="50" r="40" fill="red"/></svg>\n'
    )


def photo_heic():
    halves((64, 48), (200, 150, 50), (50, 150, 200)).save(HERE / "photo.heic", quality=50)


def chunk(kind, body):
    return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)


def bomb_png():
    # A valid PNG header claiming 20,000 x 20,000 8-bit grey pixels (400 megapixels, 1.6 GB
    # as RGBA), with the data of a few rows only. The processor must refuse it from the
    # header, before decoding anything.
    width = height = 20_000
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 0, 0, 0, 0)
    rows = b"".join(b"\x00" + b"\x00" * width for _ in range(4))
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(rows, 9)) + chunk(b"IEND", b"")
    (HERE / "bomb-20000.png").write_bytes(data)


if __name__ == "__main__":
    for make in (rotated_exif6_gps, alpha_png, photo_webp, animated_gif, wide_jpg, truncated_jpg,
                 not_an_image, drawing_svg, photo_heic, bomb_png):
        make()
    for path in sorted(HERE.iterdir()):
        if path.suffix != ".py":
            print(f"{path.name:24} {path.stat().st_size:>8} bytes")
