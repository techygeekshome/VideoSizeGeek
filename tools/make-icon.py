"""Generates the VideoSizeGeek app icon at every size the range uses.

Same house style as every other Geek app: a rounded square with a vertical blue
gradient and a single flat white glyph, no lettering. Edit the glyph function
below, never the PNGs directly, then re-run this to regenerate everything.

    python tools/make-icon.py
"""

from PIL import Image, ImageDraw
import math
import os

SIZES = [1024, 512, 256, 128]
TOP = (123, 169, 246)
BOTTOM = (47, 109, 237)
WHITE = (255, 255, 255, 255)
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "icons")


def rounded_square(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    for y in range(size):
        t = y / (size - 1)
        r = round(TOP[0] + (BOTTOM[0] - TOP[0]) * t)
        g = round(TOP[1] + (BOTTOM[1] - TOP[1]) * t)
        b = round(TOP[2] + (BOTTOM[2] - TOP[2]) * t)
        for x in range(size):
            img.putpixel((x, y), (r, g, b, 255))

    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, size - 1, size - 1], radius=round(size * 0.191), fill=255
    )
    img.putalpha(mask)
    return img


def draw_glyph(draw: ImageDraw.ImageDraw, size: int) -> None:
    """A play triangle (this is a video tool) with two shrink-corner marks, meaning
    'fits the video into a smaller frame'. Kept to one idea, no lettering, reads at
    16px in a taskbar as well as at 1024px on a store listing."""

    cx, cy = size / 2, size / 2

    # The play triangle, slightly left of centre so the shrink marks have room.
    tri_size = size * 0.30
    tx = cx - size * 0.06
    points = [
        (tx - tri_size * 0.35, cy - tri_size * 0.5),
        (tx - tri_size * 0.35, cy + tri_size * 0.5),
        (tx + tri_size * 0.55, cy),
    ]
    draw.polygon(points, fill=WHITE)

    # Two L-shaped corner marks, top-right and bottom-right, angled inward: the
    # universal "shrink to fit" mark used by every video and image tool's resize
    # handle, just doubled up and pointed at the frame instead of a handle.
    mark_len = size * 0.16
    stroke = max(2, round(size * 0.045))
    ox = cx + size * 0.20

    def corner(x0, y0, dx, dy):
        draw.line([(x0, y0), (x0 + dx * mark_len, y0)], fill=WHITE, width=stroke)
        draw.line([(x0, y0), (x0, y0 + dy * mark_len)], fill=WHITE, width=stroke)

    corner(ox + mark_len, cy - tri_size * 0.62, -1, -1)  # top-right, arm points up-left
    corner(ox + mark_len, cy + tri_size * 0.62, -1, 1)   # bottom-right, arm points down-left


def build(size: int) -> Image.Image:
    img = rounded_square(size)
    draw = ImageDraw.Draw(img)
    draw_glyph(draw, size)
    return img


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    images = {s: build(s) for s in SIZES}

    for s, img in images.items():
        img.save(os.path.join(OUT_DIR, f"videosizegeek-{s}.png"))

    # .ico frames at the standard Windows sizes, downsampled from the 1024 master.
    ico_sizes = [16, 24, 32, 48, 64, 128, 256]
    master = images[1024]
    frames = [master.resize((s, s), Image.LANCZOS) for s in ico_sizes]
    frames[0].save(
        os.path.join(OUT_DIR, "videosizegeek.ico"),
        format="ICO",
        sizes=[(s, s) for s in ico_sizes],
        append_images=frames[1:],
    )
    print(f"Wrote {len(images)} PNGs and one .ico to {OUT_DIR}")


if __name__ == "__main__":
    main()
