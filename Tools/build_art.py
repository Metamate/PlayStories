"""
PlayStories art pipeline.

* Copies / trims the hand-drawn art from RawAssets into Assets/PlayStories.
* Cuts the sheep + UI icons out of the mock-up (diff against the clean
  background + flood fill from outside the black outline).
* Generates "eyes closed" overlays so every animal can blink.
* Draws PLACEHOLDER animals (pig, hen, duck) and small decorative sprites in
  a crayon-ish style so the prototype is complete. Replace the placeholders
  by dropping real drawings into RawAssets/Animals/<name>.png and re-running.

    python Tools/build_art.py
"""
from __future__ import annotations

import math
import os
from collections import deque

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
RAW = os.path.join(ROOT, "RawAssets")
OUT_FARM = os.path.join(ROOT, "Assets", "PlayStories", "Books", "Farm", "Art")
OUT_UI = os.path.join(ROOT, "Assets", "PlayStories", "Art", "UI")
OUT_FX = os.path.join(ROOT, "Assets", "PlayStories", "Art", "FX")
OUT_COVERS = os.path.join(ROOT, "Assets", "PlayStories", "Art", "Covers")
MOCKUP = os.path.join(RAW, "App background farm field 1920w example ui.png")
BACKGROUND = os.path.join(RAW, "App background farm field 1920w.png")

OUTLINE = (24, 20, 20, 255)
rng = np.random.default_rng(1234)


def save(im: Image.Image, folder: str, name: str) -> None:
    os.makedirs(folder, exist_ok=True)
    p = os.path.join(folder, name)
    im.save(p, optimize=True)
    print("  wrote", os.path.relpath(p, ROOT), im.size)


def pad(im: Image.Image, px: int) -> Image.Image:
    out = Image.new("RGBA", (im.width + 2 * px, im.height + 2 * px), (0, 0, 0, 0))
    out.paste(im, (px, px))
    return out


def trim(im: Image.Image, px: int = 8) -> Image.Image:
    return pad(im.crop(im.getbbox()), px)


# ===================================================================== cut-outs
def flood_outside(barrier: np.ndarray) -> np.ndarray:
    h, w = barrier.shape
    out = np.zeros_like(barrier, bool)
    q: deque = deque()
    for x in range(w):
        for y in (0, h - 1):
            if not barrier[y, x] and not out[y, x]:
                out[y, x] = True; q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if not barrier[y, x] and not out[y, x]:
                out[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not out[ny, nx] and not barrier[ny, nx]:
                out[ny, nx] = True; q.append((ny, nx))
    return out


def cut_outlined(img: np.ndarray, box, dark_thr: float, dilate: int = 1) -> Image.Image:
    """Cut an object with a closed dark outline out of a flat image."""
    x0, y0, x1, y1 = box
    c = img[y0:y1, x0:x1]
    barrier = Image.fromarray(((c.mean(2) < dark_thr) * 255).astype(np.uint8))
    for _ in range(dilate):
        barrier = barrier.filter(ImageFilter.MaxFilter(3))
    mask = Image.fromarray((~flood_outside(np.array(barrier) > 0) * 255).astype(np.uint8))
    for _ in range(dilate):
        mask = mask.filter(ImageFilter.MinFilter(3))
    mask = mask.filter(ImageFilter.GaussianBlur(0.7))
    im = Image.fromarray(np.dstack([c.astype(np.uint8), np.array(mask)]), "RGBA")
    return trim(im, 6)


def cut_back_arrow(img: np.ndarray) -> Image.Image:
    """The curly back arrow sits on the red barn: keep the grey/white pixels only."""
    x0, y0, x1, y1 = 25, 40, 225, 210
    c = img[y0:y1, x0:x1]
    keep = ((c.max(2) - c.min(2)) < 40) & (c.mean(2) > 60)
    h, w = keep.shape
    lab = np.zeros((h, w), int); best = (0, 0); n = 0
    for y in range(h):
        for x in range(w):
            if keep[y, x] and not lab[y, x]:
                n += 1; q = deque([(y, x)]); lab[y, x] = n; cnt = 0
                while q:
                    cy, cx = q.popleft(); cnt += 1
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            ny, nx = cy + dy, cx + dx
                            if 0 <= ny < h and 0 <= nx < w and keep[ny, nx] and not lab[ny, nx]:
                                lab[ny, nx] = n; q.append((ny, nx))
                if cnt > best[0]:
                    best = (cnt, n)
    m = Image.fromarray(((lab == best[1]) * 255).astype(np.uint8))
    m = m.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.7))
    im = trim(Image.fromarray(np.dstack([c.astype(np.uint8), np.array(m)]), "RGBA"), 10)
    return add_outline(im, 4, (60, 60, 60, 255))


def add_outline(im: Image.Image, width: int, color) -> Image.Image:
    a = im.getchannel("A").point(lambda v: 255 if v > 100 else 0)
    for _ in range(width):
        a = a.filter(ImageFilter.MaxFilter(3))
    a = a.filter(ImageFilter.GaussianBlur(0.8))
    base = Image.new("RGBA", im.size, color)
    base.putalpha(a)
    base.alpha_composite(im)
    return base


# ===================================================================== blink overlays
def blink_overlay(src: Image.Image, eyes, line_w: int) -> Image.Image:
    """
    Build an 'eyes closed' overlay the same size as the sprite.
    eyes: list of (cx, cy, rx, ry, fill_rgb|None, line_rgb)
      fill None -> radially in-paint from the pixels just outside the eye.
    """
    arr = np.array(src).astype(np.float32)
    h, w = arr.shape[:2]
    over = np.zeros_like(arr)
    yy, xx = np.mgrid[0:h, 0:w]
    for cx, cy, rx, ry, fill, line in eyes:
        nx = (xx - cx) / rx; ny = (yy - cy) / ry
        r = np.sqrt(nx * nx + ny * ny)
        inside = r <= 1.0
        if fill is None:
            ang = np.arctan2(ny, nx)
            sx = np.clip((cx + np.cos(ang) * rx * 1.18).round().astype(int), 0, w - 1)
            sy = np.clip((cy + np.sin(ang) * ry * 1.18).round().astype(int), 0, h - 1)
            over[inside] = arr[sy[inside], sx[inside]]
        else:
            over[inside, :3] = fill
        over[inside, 3] = 255
        # soft edge
        edge = (r > 0.92) & (r <= 1.0)
        over[edge, 3] = 255 * (1.0 - (r[edge] - 0.92) / 0.08)
    im = Image.fromarray(over.clip(0, 255).astype(np.uint8), "RGBA")
    im = im.filter(ImageFilter.SMOOTH)
    d = ImageDraw.Draw(im)
    for cx, cy, rx, ry, fill, line in eyes:
        # happy closed eye: a downward-curved arc  ︶ slightly below centre
        bb = (cx - rx * 0.95, cy - ry * 0.55, cx + rx * 0.95, cy + ry * 0.55)
        d.arc(bb, 15, 165, fill=tuple(line) + (255,), width=line_w)
    return im


# ===================================================================== crayon drawing kit
def crayon_texture(w: int, h: int, strength: float = 0.07) -> np.ndarray:
    """Directional noise that looks a bit like crayon / pencil strokes (multiplier ~1)."""
    W = H = int(math.hypot(w, h)) + 4          # oversize so rotation leaves no empty corners
    n = rng.random((H // 2 + 1, W // 2 + 1)).astype(np.float32)
    img = Image.fromarray((n * 255).astype(np.uint8)).resize((W * 3, H // 2 + 1), Image.BILINEAR)
    img = img.filter(ImageFilter.GaussianBlur(1.2)).resize((W, H), Image.BILINEAR).rotate(25, resample=Image.BILINEAR)
    l, t0 = (W - w) // 2, (H - h) // 2
    img = img.crop((l, t0, l + w, t0 + h))
    t = np.array(img).astype(np.float32) / 255.0
    t = (t - t.mean()) / (t.std() + 1e-6)
    return 1.0 + t * strength


class Painter:
    def __init__(self, w: int, h: int):
        self.w, self.h = w, h
        self.im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.tex = crayon_texture(w, h)

    def mask(self):
        return Image.new("L", (self.w, self.h), 0)

    def shape(self, mask: Image.Image, color, outline: int = 12):
        """Fill a mask with a textured colour and a rounded black outline around it."""
        if outline:
            o = mask.filter(ImageFilter.GaussianBlur(outline * 0.55)).point(lambda v: 255 if v > 6 else 0)
            o = o.filter(ImageFilter.GaussianBlur(1.0))
            layer = Image.new("RGBA", (self.w, self.h), OUTLINE)
            layer.putalpha(o)
            self.im.alpha_composite(layer)
        col = np.zeros((self.h, self.w, 4), np.float32)
        col[..., :3] = np.array(color[:3], np.float32) * self.tex[..., None]
        col[..., 3] = np.array(mask.filter(ImageFilter.GaussianBlur(0.8)), np.float32)
        self.im.alpha_composite(Image.fromarray(col.clip(0, 255).astype(np.uint8), "RGBA"))

    def ellipse(self, bb, color, outline=12):
        m = self.mask(); ImageDraw.Draw(m).ellipse(bb, fill=255); self.shape(m, color, outline)

    def polygon(self, pts, color, outline=12, round_px=0):
        m = self.mask(); ImageDraw.Draw(m).polygon(pts, fill=255)
        if round_px:
            m = m.filter(ImageFilter.GaussianBlur(round_px)).point(lambda v: 255 if v > 128 else 0)
        self.shape(m, color, outline)

    def union(self, ops, color, outline=12):
        m = self.mask(); d = ImageDraw.Draw(m)
        for kind, arg in ops:
            if kind == "e": d.ellipse(arg, fill=255)
            elif kind == "p": d.polygon(arg, fill=255)
            elif kind == "r": d.rounded_rectangle(arg[0], arg[1], fill=255)
        self.shape(m, color, outline)

    def stroke(self, pts, color, width, outline=8):
        if outline:
            ImageDraw.Draw(self.im).line(pts, fill=OUTLINE, width=width + outline * 2, joint="curve")
            for p in (pts[0], pts[-1]):
                r = (width + outline * 2) / 2
                ImageDraw.Draw(self.im).ellipse((p[0] - r, p[1] - r, p[0] + r, p[1] + r), fill=OUTLINE)
        ImageDraw.Draw(self.im).line(pts, fill=tuple(color) + (255,), width=width, joint="curve")
        for p in (pts[0], pts[-1]):
            r = width / 2
            ImageDraw.Draw(self.im).ellipse((p[0] - r, p[1] - r, p[0] + r, p[1] + r), fill=tuple(color) + (255,))

    def eye(self, cx, cy, rx=24, ry=32):
        d = ImageDraw.Draw(self.im)
        d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=OUTLINE)
        d.ellipse((cx - rx * 0.55 - 6, cy - ry * 0.6 - 6, cx - rx * 0.55 + 7, cy - ry * 0.6 + 7), fill=(255, 255, 255, 255))

    def blush(self, cx, cy, rx=36, ry=20, color=(240, 120, 140)):
        layer = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        ImageDraw.Draw(layer).ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=color + (110,))
        self.im.alpha_composite(layer.filter(ImageFilter.GaussianBlur(5)))

    def arc(self, bb, a0, a1, width=7, color=OUTLINE):
        ImageDraw.Draw(self.im).arc(bb, a0, a1, fill=color, width=width)

    def result(self, scale: float = 0.6) -> Image.Image:
        im = trim(self.im, 10)
        return im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)


# ===================================================================== placeholder animals
PIG = (247, 184, 196); PIG_DARK = (232, 140, 160); HOOF = (170, 105, 115)


def draw_pig():
    p = Painter(1200, 1050)
    # curly tail
    pts = [(940 + 40 * math.cos(t) * (1 - t / 14), 470 + 40 * math.sin(t) * (1 - t / 14)) for t in np.linspace(0, 9, 40)]
    pts = [(920, 520)] + pts
    p.stroke(pts, PIG_DARK, 14, 7)
    # far legs (slightly darker)
    for x in (560, 800):
        p.union([("r", ((x, 690, x + 80, 900), 30))], PIG_DARK, 11)
        p.union([("r", ((x, 860, x + 80, 905), 18))], HOOF, 0)
    # ears behind head
    p.polygon([(175, 330), (150, 165), (300, 255)], PIG_DARK, 11, round_px=6)
    p.polygon([(420, 255), (545, 160), (555, 320)], PIG_DARK, 11, round_px=6)
    # body
    p.ellipse((380, 350, 980, 830), PIG, 12)
    # near legs
    for x in (440, 720):
        p.union([("r", ((x, 720, x + 85, 940), 32))], PIG, 11)
        p.union([("r", ((x, 895, x + 85, 945), 18))], HOOF, 0)
    # head
    p.ellipse((130, 240, 610, 700), PIG, 12)
    # snout
    p.ellipse((262, 495, 462, 635), (244, 156, 176), 10)
    d = ImageDraw.Draw(p.im)
    d.ellipse((310, 535, 340, 590), fill=(138, 74, 88, 255))
    d.ellipse((385, 535, 415, 590), fill=(138, 74, 88, 255))
    eyes = [(262, 430), (462, 430)]
    for cx, cy in eyes:
        p.eye(cx, cy)
    p.blush(190, 540); p.blush(535, 540)
    p.arc((330, 625, 395, 675), 20, 160)
    return p, [(x, y, 32, 40, PIG, OUTLINE[:3]) for x, y in eyes]


HEN = (251, 247, 238); HEN_SHADE = (236, 230, 216); RED = (229, 72, 77); ORANGE = (242, 163, 58)


def draw_hen():
    p = Painter(1050, 1150)
    # legs
    for x in (450, 590):
        p.stroke([(x, 850), (x, 1010)], ORANGE, 18, 7)
        for dx in (-45, 0, 45):
            p.stroke([(x, 1010), (x + dx, 1050)], ORANGE, 14, 6)
    # tail feathers
    p.union([("e", (700, 280, 860, 470)), ("e", (780, 360, 930, 560)), ("e", (640, 230, 780, 400))], HEN, 12)
    # comb
    p.union([("e", (285, 85, 385, 185)), ("e", (350, 60, 460, 170)), ("e", (425, 90, 520, 185))], RED, 11)
    # body + head as one smooth shape
    p.union([("e", (230, 430, 860, 920)), ("e", (230, 140, 620, 530)), ("p", [(260, 400), (600, 400), (700, 600), (260, 650)])], HEN, 13)
    # wing
    p.polygon([(520, 560), (790, 590), (760, 730), (640, 790), (540, 720)], HEN_SHADE, 9, round_px=14)
    p.arc((560, 640, 760, 760), 200, 330, 6)
    # beak + wattle
    p.ellipse((215, 400, 275, 500), RED, 9)
    p.polygon([(240, 320), (135, 360), (245, 400)], ORANGE, 10, round_px=4)
    eyes = [(320, 290), (470, 300)]
    for cx, cy in eyes:
        p.eye(cx, cy, 22, 30)
    p.blush(300, 380, 30, 18); p.blush(520, 385, 30, 18)
    return p, [(x, y, 30, 38, HEN, OUTLINE[:3]) for x, y in eyes]


DUCK = (255, 217, 90); DUCK_SHADE = (247, 196, 60); BILL = (243, 154, 43)


def draw_duck():
    p = Painter(1100, 1000)
    # feet
    p.ellipse((380, 860, 520, 935), BILL, 10)
    p.ellipse((580, 860, 720, 935), BILL, 10)
    # tail
    p.polygon([(820, 600), (950, 470), (900, 660)], DUCK, 11, round_px=8)
    # body + head
    p.union([("e", (300, 500, 920, 900)), ("e", (240, 150, 620, 530)), ("p", [(330, 420), (560, 420), (620, 620), (320, 640)])], DUCK, 13)
    # hair tuft
    p.stroke([(430, 160), (450, 95), (500, 80)], DUCK, 14, 7)
    p.stroke([(400, 165), (395, 105)], DUCK, 12, 7)
    # wing
    p.polygon([(560, 610), (820, 620), (800, 760), (690, 810), (590, 760)], DUCK_SHADE, 9, round_px=14)
    # bill
    p.ellipse((110, 375, 330, 470), BILL, 10)
    p.arc((140, 395, 310, 455), 10, 170, 5)
    eyes = [(330, 330), (480, 330)]
    for cx, cy in eyes:
        p.eye(cx, cy, 22, 30)
    p.blush(300, 400, 30, 18); p.blush(540, 405, 30, 18)
    return p, [(x, y, 30, 38, DUCK, OUTLINE[:3]) for x, y in eyes]


def finish_generated(name: str, painter: Painter, eyes, scale: float = 0.6):
    """Save sprite + blink overlay with identical trimming."""
    bbox = painter.im.getbbox()
    base = pad(painter.im.crop(bbox), 10)
    eyes_shift = [(x - bbox[0] + 10, y - bbox[1] + 10, rx, ry, f, l) for x, y, rx, ry, f, l in eyes]
    blink = blink_overlay(base, eyes_shift, 9)
    size = (int(base.width * scale), int(base.height * scale))
    save(base.resize(size, Image.LANCZOS), OUT_FARM, f"{name}.png")
    save(blink.resize(size, Image.LANCZOS), OUT_FARM, f"{name}_blink.png")


# ===================================================================== fx + ui sprites
def soft_shadow():
    w, h = 512, 128
    im = Image.new("L", (w, h), 0)
    ImageDraw.Draw(im).ellipse((40, 24, w - 40, h - 24), fill=255)
    im = im.filter(ImageFilter.GaussianBlur(18))
    out = Image.new("RGBA", (w, h), (20, 40, 10, 0)); out.putalpha(im.point(lambda v: v * 0.55))
    return out


def heart():
    p = Painter(400, 380)
    p.union([("e", (40, 40, 210, 210)), ("e", (190, 40, 360, 210)), ("p", [(52, 165), (348, 165), (200, 340)])], (244, 96, 122), 12)
    ImageDraw.Draw(p.im).ellipse((95, 85, 135, 125), fill=(255, 255, 255, 200))
    return p.result(0.5)


def star(points=5, color=(255, 205, 64), inner=0.48, size=400):
    p = Painter(size, size)
    cx = cy = size / 2; R = size * 0.42
    pts = []
    for i in range(points * 2):
        r = R if i % 2 == 0 else R * inner
        a = -math.pi / 2 + i * math.pi / points
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    p.polygon(pts, color, 12, round_px=5)
    return p.result(0.5)


def sparkle():
    size = 256; im = Image.new("L", (size, size), 0); d = ImageDraw.Draw(im)
    c = size / 2
    d.polygon([(c, 8), (c + 22, c - 22), (size - 8, c), (c + 22, c + 22), (c, size - 8), (c - 22, c + 22), (8, c), (c - 22, c - 22)], fill=255)
    im = im.filter(ImageFilter.GaussianBlur(3))
    glow = Image.new("L", (size, size), 0); ImageDraw.Draw(glow).ellipse((c - 50, c - 50, c + 50, c + 50), fill=160)
    glow = glow.filter(ImageFilter.GaussianBlur(25))
    a = np.maximum(np.array(im), np.array(glow))
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0)); out.putalpha(Image.fromarray(a))
    return out


def music_note():
    p = Painter(400, 460)
    p.ellipse((60, 300, 200, 410), (70, 70, 110), 10)
    p.ellipse((230, 260, 370, 370), (70, 70, 110), 10)
    p.polygon([(175, 345), (195, 345), (195, 90), (175, 90)], (70, 70, 110), 10)
    p.polygon([(345, 305), (365, 305), (365, 50), (345, 50)], (70, 70, 110), 10)
    p.polygon([(175, 80), (365, 40), (365, 110), (175, 150)], (70, 70, 110), 10)
    return p.result(0.5)


def cloud():
    w, h = 900, 400
    m = Image.new("L", (w, h), 0); d = ImageDraw.Draw(m)
    for bb in [(60, 170, 330, 380), (200, 70, 520, 370), (420, 110, 700, 380), (600, 190, 850, 380), (120, 260, 780, 380)]:
        d.ellipse(bb, fill=255)
    m = m.filter(ImageFilter.GaussianBlur(4))
    tex = crayon_texture(w, h, 0.05)
    col = np.zeros((h, w, 4), np.float32)
    col[..., :3] = np.array([255, 255, 255], np.float32) * np.clip(tex[..., None], 0, 1)
    col[..., 3] = np.array(m, np.float32) * 0.92
    im = Image.fromarray(col.clip(0, 255).astype(np.uint8), "RGBA")
    return trim(im, 4).resize((450, 200), Image.LANCZOS)


def butterfly():
    p = Painter(500, 400)
    wing = (255, 166, 77); wing2 = (255, 205, 120)
    p.ellipse((40, 40, 240, 230), wing, 10); p.ellipse((260, 40, 460, 230), wing, 10)
    p.ellipse((80, 190, 235, 340), wing2, 10); p.ellipse((265, 190, 420, 340), wing2, 10)
    p.ellipse((225, 90, 275, 330), (90, 70, 70), 8)
    p.stroke([(240, 100), (200, 30)], (90, 70, 70), 6, 0); p.stroke([(260, 100), (300, 30)], (90, 70, 70), 6, 0)
    return p.result(0.4)


def bubble():
    """Speech bubble with a little tail pointing down-left (towards the animal's head)."""
    p = Painter(560, 400)
    p.union([("r", ((30, 30, 530, 300), 110)), ("p", [(120, 250), (230, 270), (70, 380)])], (255, 255, 255), 12)
    return p.result(0.7)


def rounded_panel(fill=(255, 250, 240), outline=10, radius=60, size=(260, 260)):
    p = Painter(size[0] + 40, size[1] + 40)
    p.tex = np.ones_like(p.tex)  # flat fill: the centre of a 9-sliced sprite gets stretched
    p.union([("r", ((20, 20, size[0] + 20, size[1] + 20), radius))], fill, outline)
    return p.im.crop(p.im.getbbox())


def ring(size=256, thickness=26):
    im = Image.new("L", (size * 2, size * 2), 0)
    d = ImageDraw.Draw(im)
    d.ellipse((4, 4, size * 2 - 4, size * 2 - 4), fill=255)
    d.ellipse((4 + thickness * 2, 4 + thickness * 2, size * 2 - 4 - thickness * 2, size * 2 - 4 - thickness * 2), fill=0)
    im = im.resize((size, size), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0)); out.putalpha(im)
    return out


def circle(size=256):
    im = Image.new("L", (size * 2, size * 2), 0)
    ImageDraw.Draw(im).ellipse((2, 2, size * 2 - 2, size * 2 - 2), fill=255)
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0)); out.putalpha(im.resize((size, size), Image.LANCZOS))
    return out


def close_icon():
    p = Painter(300, 300)
    p.union([("r", ((30, 30, 270, 270), 50))], (246, 235, 221), 12)
    p.stroke([(95, 95), (205, 205)], (200, 64, 52), 26, 7)
    p.stroke([(205, 95), (95, 205)], (200, 64, 52), 26, 7)
    return p.result(0.6)


def gear_icon():
    p = Painter(400, 400)
    c = 200; pts = []
    teeth = 8
    for i in range(teeth * 4):
        a = i * 2 * math.pi / (teeth * 4)
        r = 170 if (i % 4) in (0, 1) else 130
        pts.append((c + r * math.cos(a), c + r * math.sin(a)))
    p.polygon(pts, (110, 150, 90), 12, round_px=4)
    p.ellipse((c - 55, c - 55, c + 55, c + 55), (62, 95, 51), 10)
    return p.result(0.45)


def play_icon():
    p = Painter(400, 400)
    p.ellipse((20, 20, 380, 380), (255, 205, 64), 12)
    p.polygon([(155, 110), (155, 290), (300, 200)], (255, 255, 255), 10, round_px=6)
    return p.result(0.5)


def replay_icon():
    p = Painter(400, 400)
    p.ellipse((20, 20, 380, 380), (120, 200, 120), 12)
    d = ImageDraw.Draw(p.im)
    d.arc((100, 100, 300, 300), 40, 320, fill=OUTLINE, width=52)
    d.arc((100, 100, 300, 300), 40, 320, fill=(255, 255, 255, 255), width=30)
    p.polygon([(270, 80), (330, 175), (225, 175)], (255, 255, 255), 9, round_px=3)
    return p.result(0.5)


def filter_icon():
    p = Painter(400, 400)
    p.polygon([(60, 80), (340, 80), (230, 210), (230, 330), (170, 300), (170, 210)], (255, 250, 240), 12, round_px=6)
    return p.result(0.45)


def lock_icon():
    p = Painter(400, 440)
    ImageDraw.Draw(p.im).arc((110, 40, 290, 240), 180, 360, fill=OUTLINE, width=50)
    ImageDraw.Draw(p.im).arc((110, 40, 290, 240), 180, 360, fill=(200, 200, 210, 255), width=26)
    p.union([("r", ((70, 160, 330, 400), 40))], (250, 200, 70), 12)
    ImageDraw.Draw(p.im).ellipse((180, 240, 220, 280), fill=OUTLINE)
    return p.result(0.4)


# ===================================================================== placeholder covers
def cover_farm(bg: Image.Image, cow: Image.Image, sheep: Image.Image) -> Image.Image:
    w, h = 800, 600
    crop = bg.crop((200, 150, 200 + 1450, 150 + 1088)).resize((w, h), Image.LANCZOS)
    c = crop.copy()
    s = sheep.resize((int(sheep.width * 0.55), int(sheep.height * 0.55)), Image.LANCZOS)
    k = cow.resize((int(cow.width * 0.7), int(cow.height * 0.7)), Image.LANCZOS)
    c.alpha_composite(s, (430, 600 - s.height - 40))
    c.alpha_composite(k, (130, 600 - k.height - 50))
    return c


def cover_simple(bg_color, draw_fn) -> Image.Image:
    w, h = 800, 600
    tex = crayon_texture(w, h, 0.05)
    col = np.zeros((h, w, 4), np.float32)
    col[..., :3] = np.array(bg_color, np.float32) * tex[..., None]; col[..., 3] = 255
    im = Image.fromarray(col.clip(0, 255).astype(np.uint8), "RGBA")
    art = draw_fn()
    art.thumbnail((520, 420), Image.LANCZOS)
    im.alpha_composite(art, ((w - art.width) // 2, (h - art.height) // 2 + 20))
    return im


def draw_bus():
    p = Painter(1000, 600)
    p.union([("r", ((80, 100, 920, 470), 70))], (255, 204, 64), 13)
    for i in range(4):
        x = 300 + i * 150
        p.union([("r", ((x, 150, x + 110, 270), 20))], (150, 210, 240), 9)
    p.union([("r", ((130, 150, 250, 420), 20))], (150, 210, 240), 9)
    p.union([("r", ((80, 320, 920, 350), 4))], (120, 90, 60), 0)
    for x in (270, 730):
        p.ellipse((x - 85, 400, x + 85, 570), (90, 90, 100), 12)
        p.ellipse((x - 35, 450, x + 35, 520), (200, 200, 210), 8)
    return p.result(0.6)


def draw_shirt():
    p = Painter(800, 700)
    p.polygon([(250, 80), (330, 80), (400, 140), (470, 80), (550, 80), (740, 230), (650, 340), (580, 290),
               (580, 640), (220, 640), (220, 290), (150, 340), (60, 230)], (120, 180, 255), 13, round_px=10)
    p.ellipse((370, 250, 430, 310), (255, 205, 64), 8)
    return p.result(0.6)


def draw_smiley():
    p = Painter(700, 700)
    p.ellipse((50, 50, 650, 650), (255, 214, 90), 13)
    p.eye(260, 290, 32, 44); p.eye(440, 290, 32, 44)
    p.blush(200, 420, 50, 28); p.blush(500, 420, 50, 28)
    p.arc((230, 330, 470, 520), 20, 160, 16)
    return p.result(0.6)


def draw_sun():
    p = Painter(700, 700)
    for i in range(10):
        a = i * math.pi / 5
        p.stroke([(350 + 220 * math.cos(a), 350 + 220 * math.sin(a)), (350 + 310 * math.cos(a), 350 + 310 * math.sin(a))], (255, 190, 60), 30, 8)
    p.ellipse((170, 170, 530, 530), (255, 214, 90), 13)
    p.eye(290, 320, 22, 30); p.eye(410, 320, 22, 30)
    p.arc((290, 350, 410, 440), 20, 160, 10)
    return p.result(0.6)


# ===================================================================== main
def main():
    print("Backgrounds")
    bg = Image.open(BACKGROUND).convert("RGBA")
    save(bg, OUT_FARM, "farm_background.png")

    mock = np.array(Image.open(MOCKUP).convert("RGB")).astype(int)

    print("Animals (hand drawn)")
    cow_src = Image.open(os.path.join(RAW, "App Asset Animal Farm Cow.png")).convert("RGBA")
    bb = cow_src.getbbox()
    cow = pad(cow_src.crop(bb), 10)
    ox, oy = bb[0] - 10, bb[1] - 10
    # eye centres measured on the original drawing
    cow_eyes = [(386 - ox, 313 - oy, 22, 28, None, (110, 110, 110)),   # left eye sits on the black patch
                (480 - ox, 317 - oy, 27, 34, (255, 255, 255), OUTLINE[:3])]
    save(cow, OUT_FARM, "cow.png")
    save(blink_overlay(cow, cow_eyes, 6), OUT_FARM, "cow_blink.png")

    sheep = cut_outlined(mock, (600, 370, 1310, 1010), 90, dilate=2)
    # sheep bbox in the crop: re-derive eye centres relative to the trimmed sprite
    sheep_eyes = [(165, 205, 30, 37, (242, 207, 184), OUTLINE[:3]), (296, 211, 29, 37, (242, 207, 184), OUTLINE[:3])]
    save(sheep, OUT_FARM, "sheep.png")
    save(blink_overlay(sheep, sheep_eyes, 6), OUT_FARM, "sheep_blink.png")

    print("Animals (placeholders)")
    for name, fn in (("pig", draw_pig), ("hen", draw_hen), ("duck", draw_duck)):
        override = os.path.join(RAW, "Animals", f"{name}.png")
        if os.path.exists(override):
            print("  using hand drawn", override)
            save(trim(Image.open(override).convert("RGBA"), 10), OUT_FARM, f"{name}.png")
            continue
        painter, eyes = fn()
        finish_generated(name, painter, eyes)

    print("UI icons")
    save(cut_outlined(mock, (90, 575, 300, 805), 110), OUT_UI, "arrow_left.png")
    save(cut_outlined(mock, (1625, 590, 1835, 815), 110), OUT_UI, "arrow_right.png")
    save(cut_outlined(mock, (1660, 1135, 1895, 1320), 80), OUT_UI, "speaker.png")
    save(cut_back_arrow(mock), OUT_UI, "back.png")
    save(close_icon(), OUT_UI, "close.png")
    save(gear_icon(), OUT_UI, "gear.png")
    save(play_icon(), OUT_UI, "play.png")
    save(replay_icon(), OUT_UI, "replay.png")
    save(filter_icon(), OUT_UI, "filter.png")
    save(lock_icon(), OUT_UI, "lock.png")
    save(rounded_panel(), OUT_UI, "panel_rounded.png")
    save(rounded_panel((255, 255, 255), 0, 60), OUT_UI, "panel_plain.png")
    save(ring(), OUT_UI, "ring.png")
    save(circle(), OUT_UI, "circle.png")
    save(bubble(), OUT_UI, "bubble.png")

    print("FX")
    save(soft_shadow(), OUT_FX, "shadow.png")
    save(heart(), OUT_FX, "heart.png")
    save(star(), OUT_FX, "star.png")
    save(sparkle(), OUT_FX, "sparkle.png")
    save(music_note(), OUT_FX, "note.png")
    save(cloud(), OUT_FX, "cloud.png")
    save(butterfly(), OUT_FX, "butterfly.png")

    print("Covers")
    save(cover_farm(bg, cow, sheep), OUT_COVERS, "cover_farm.png")
    save(cover_simple((150, 210, 240), draw_bus), OUT_COVERS, "cover_bus.png")
    save(cover_simple((255, 225, 200), draw_shirt), OUT_COVERS, "cover_clothes.png")
    save(cover_simple((255, 190, 200), draw_smiley), OUT_COVERS, "cover_feelings.png")
    save(cover_simple((190, 230, 255), draw_sun), OUT_COVERS, "cover_weather.png")
    print("done")


if __name__ == "__main__":
    main()
