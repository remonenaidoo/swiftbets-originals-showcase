"""The four new slots' renders into game art. Run with the rembg venv from this folder: ~/.venvs/rembg/bin/python process.py
Symbols are cut out (rembg isnet-general-use), cropped to their solid pixels and scaled UP to fill a 512 px cell;
Sardine Run's x2 / x3 Wilds get a badge (as Cape Point Ways); Kimberley Hold gets a blank hold tile and a coin with a
clear gold centre; Fynbos Bloom's scatter gets a flat-topped Table Mountain painted in; logos are cut out;
backgrounds are resized for landscape and portrait."""
import os, re
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from rembg import new_session, remove
import scipy.ndimage as ndi

RAW, PUB, SIZE = 'raw', '../../public/assets', 512
FONT = '/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf'
GAMES = {'star': 'karoo-stargazer', 'run': 'sardine-run', 'kim': 'kimberley-hold', 'fyn': 'fynbos-bloom'}
# symbol file -> raw render (chosen variant)
SYMBOLS = {
    'star': {'leopard': 'star-leopard', 'kudu': 'star-kudu-s11', 'meerkat': 'star-meerkat-s11', 'ostrich': 'star-ostrich-s11',
             'ace': 'star-ace', 'king': 'star-king', 'queen': 'star-queen', 'jack': 'star-jack', 'ten': 'star-ten',
             'wild': 'star-wild', 'scatter': 'star-scatter'},
    'run': {'dolphin': 'run-dolphin', 'gannet': 'run-gannet', 'seal': 'run-seal', 'shoal': 'run-shoal', 'hook': 'run-hook-s29',
            'buoy': 'run-buoy', 'mussel': 'run-mussel', 'crab': 'run-crab', 'wild': 'run-wild-s29', 'scatter': 'run-scatter'},
    'kim': {'prospector': 'kim-prospector', 'headgear': 'kim-headgear', 'mule': 'kim-mule-s11', 'lantern': 'kim-lantern',
            'pink': 'kim-pink', 'blue': 'kim-blue', 'green': 'kim-green', 'yellow': 'kim-yellow', 'wild': 'kim-wild', 'coin': 'kim-coin'},
    'fyn': {'kingprotea': 'fyn-kingprotea', 'pincushion': 'fyn-pincushion', 'disa': 'fyn-disa', 'erica': 'fyn-erica', 'buchu': 'fyn-buchu',
            'restio': 'fyn-restio', 'sunbird': 'fyn-sunbird', 'seedpod': 'fyn-seedpod', 'dewdrop': 'fyn-dewdrop', 'sun': 'fyn-sun', 'bloom': 'fyn-bloom'},
}
BG_BARS = {'kim-bg-landscape': (92, 98)}
LOGOS = {'star': 'star-logo', 'run': 'run-logo', 'kim': 'kim-logo', 'fyn': 'fyn-logo'}
session = new_session('isnet-general-use')


def coin_raw():
    """The diamond token came back with a black centre: fill it with polished gold, darker in the middle so the
    printed value reads, like Highveld Hold's coin."""
    im = Image.open(f'{RAW}/kim-coin.png').convert('RGB')
    cx, cy, r = 514, 508, 312
    yy, xx = np.mgrid[0:im.height, 0:im.width]
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / r
    sheen = np.clip(1 - np.sqrt((xx - cx + r * .35) ** 2 + (yy - cy + r * .45) ** 2) / (r * 1.1), 0, 1) ** 2
    k = np.clip(0.55 + 0.45 * d + 0.35 * sheen, 0, 1.25)[..., None]
    gold = np.array([150, 98, 22], np.float32) * k
    rim_shadow = np.clip((d - 0.86) / 0.14, 0, 1)[..., None]
    gold = gold * (1 - 0.55 * rim_shadow)
    a = np.array(im).astype(np.float32)
    m = (d < 1.0)[..., None]
    out = np.where(m, np.clip(gold, 0, 255), a)
    # Soft blend across the seam with the original bevel.
    mask = Image.fromarray(((d < 1.0) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3))
    return Image.composite(Image.fromarray(out.astype(np.uint8)), im, mask)


def sun_raw():
    """The scatter came back with a pointed peak: repaint the ring's inside as a sun rising behind a flat-topped
    Table Mountain (Devil's Peak left, Lion's Head right), textured with the render's own rock."""
    im = Image.open(f'{RAW}/fyn-sun.png').convert('RGB')
    a = np.array(im).astype(np.float32)
    H, W = im.height, im.width
    cx, cy, R = 512, 496, 352
    yy, xx = np.mgrid[0:H, 0:W]
    # Sky: warm amber glow around the sun, darker toward the ring.
    sx, sy, sr = 512, 360, 150
    ds = np.sqrt((xx - sx) ** 2 + (yy - sy) ** 2)
    glow = np.clip(1 - (ds - sr) / 330, 0, 1) ** 1.6
    sky = np.dstack([40 + 200 * glow, 18 + 130 * glow, 0 + 10 * glow])
    disc = np.clip((sr - ds) / 4 + 0.5, 0, 1)[..., None]
    core = np.clip(1 - ds / sr, 0, 1)[..., None]
    sun = np.dstack([np.full_like(ds, 255), 236 + 18 * core[..., 0], 90 + 120 * core[..., 0]])
    sky = sky * (1 - disc) + sun * disc
    # Rays
    ang = np.arctan2(yy - sy, xx - sx)
    rays = (np.cos(ang * 36) * .5 + .5) ** 6 * np.clip(1 - (ds - sr) / 300, 0, 1) * (ds > sr - 10)
    rays = np.array(Image.fromarray((rays * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(6))).astype(np.float32) / 255
    sky = sky + np.dstack([rays * 70, rays * 45, rays * 8])
    # Mountain silhouette (x, y) in raw pixels; flat top at y=420.
    prof = [(150, 610), (190, 560), (235, 470), (262, 440), (285, 455), (318, 430), (345, 420), (680, 418), (700, 425),
            (716, 470), (735, 505), (770, 512), (790, 470), (815, 452), (840, 462), (870, 520), (880, 640), (150, 640)]
    mmask = Image.new('L', (W, H)); ImageDraw.Draw(mmask).polygon(prof, fill=255)
    mmask = np.array(mmask.filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32)[..., None] / 255
    rock = np.roll(a, -60, axis=0) * 0.9 + 6  # the render's own dark rock, shifted up
    # Golden rim light along the top edge of the silhouette.
    edge = Image.fromarray((mmask[..., 0] * 255).astype(np.uint8))
    rim = (np.array(edge).astype(np.float32) - np.array(edge.filter(ImageFilter.GaussianBlur(5))).astype(np.float32))
    rim = np.clip(rim / 60, 0, 1)[..., None] * (yy < 560)[..., None]
    inner = sky * (1 - mmask) + rock * mmask + np.array([255, 190, 60]) * rim * 0.8
    # Keep the render's foreground hills (below y~600) and the ring itself.
    fg = Image.new('L', (W, H)); ImageDraw.Draw(fg).rectangle((0, 600, W, H), fill=255)
    fg = np.array(fg.filter(ImageFilter.GaussianBlur(20))).astype(np.float32)[..., None] / 255
    inner = inner * (1 - fg) + a * fg
    dr = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    ring = np.clip((R - dr) / 3, 0, 1)[..., None]
    out = a * (1 - ring) + np.clip(inner, 0, 255) * ring
    return Image.fromarray(out.astype(np.uint8))


SPECIAL = {'kim-coin': coin_raw, 'fyn-sun': sun_raw}


# Glowing renders on black lose their light to rembg: alpha also comes from brightness (as Cape Point Ways' logo).
GLOW = {'run-scatter', 'fyn-bloom', 'fyn-sun'}


def bright_alpha(src):
    rgb = np.array(src).astype(np.float32)
    return np.clip((rgb.max(axis=2) - 10) * 4, 0, 255)


def disc(src, cx, cy, r):
    m = Image.new('L', src.size); ImageDraw.Draw(m).ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    return np.array(m.filter(ImageFilter.GaussianBlur(1.5))).astype(np.float32)


def clean(name, src, a):
    """Per-render cut-out repairs, found by inspecting each symbol at full size."""
    rgb = np.array(src).astype(np.float32)
    name = re.sub(r'-s\d+$', '', name)  # chosen seed variants share the base render's repairs
    if name == 'run-scatter':  # only the glow, not the dark haze around the water
        a = np.maximum(np.array(remove(src, session=session).getchannel('A')).astype(np.float32), np.clip((rgb.max(axis=2) - 30) * 3, 0, 255))
    if name == 'fyn-sun':  # the whole scene inside the ring is the symbol
        a = np.maximum(a, disc(src, 512, 496, 372))
    if name == 'star-kudu':  # a thin leftover of the render's ring above the horns
        keep = ndi.binary_opening(a > 60, structure=np.ones((9, 9)))
        keep = ndi.binary_dilation(keep, iterations=3)
        a = a * keep
    if name == 'star-queen':  # the Q's black counter (enclosed, so rembg kept it)
        dark = rgb.max(axis=2) < 28
        lab, n = ndi.label(dark)
        border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])))
        for i in range(1, n + 1):
            if i not in border and (lab == i).sum() > 2000:
                a = np.where(ndi.binary_dilation(lab == i, iterations=2), 0, a)
    if name == 'star-leopard':  # the bust's smoky bottom becomes a clean fade
        y0, y1 = 0.72 * a.shape[0], 0.97 * a.shape[0]
        fade = np.clip((y1 - np.arange(a.shape[0])) / (y1 - y0), 0, 1)[:, None]
        a = a * fade
    if name == 'run-dolphin':  # faint spray smudge under the tail
        a = np.clip((a - 50) * 255 / 205, 0, 255)
    if name == 'fyn-restio':  # dark fuzz around the seed heads
        a = a * np.clip((rgb.max(axis=2) - 25) / 45, 0, 1)
    return a


def cutout(name, solid=60):
    src = SPECIAL[name]() if name in SPECIAL else Image.open(f'{RAW}/{name}.png').convert('RGB')
    im = remove(src, session=session).convert('RGBA')
    a = np.array(im.getchannel('A')).astype(np.float32)
    if name in GLOW:
        a = np.maximum(a, bright_alpha(src))
    a = clean(name, src, a)
    im = Image.fromarray(np.dstack([np.array(src), a.astype(np.uint8)]), 'RGBA')
    return im.crop(Image.fromarray((a > solid).astype(np.uint8) * 255).getbbox())


def fitted(im, scale=0.94):
    # Scaled to fit the box either way (thumbnail would never enlarge a small cut-out).
    k = SIZE * scale / max(im.width, im.height)
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
    c = Image.new('RGBA', (SIZE, SIZE)); c.alpha_composite(im, ((SIZE - im.width) // 2, (SIZE - im.height) // 2)); return c


def badge(c, text, fill=(10, 40, 60, 235), ring=(120, 240, 255, 255), ink=(200, 250, 255, 255)):
    # Cape Point Ways' badge, scaled from its 768 px cell to 512.
    s = SIZE / 768
    d = ImageDraw.Draw(c)
    f = ImageFont.truetype(FONT, round(116 * s))
    w = d.textlength(text, font=f) + 88 * s; h = 148 * s
    x0, y0 = SIZE - w - 20 * s, SIZE - h - 20 * s
    d.rounded_rectangle((x0, y0, x0 + w, y0 + h), radius=h / 2, fill=fill, outline=ring, width=round(10 * s))
    d.text((x0 + w / 2, y0 + h / 2 + 4 * s), text, font=f, fill=ink, anchor='mm', stroke_width=round(4 * s), stroke_fill=(0, 30, 50, 255))
    return c


if __name__ == '__main__':
    for g, game in GAMES.items():
        out = f'{PUB}/{game}/symbols'
        os.makedirs(out, exist_ok=True)
        for name, raw in SYMBOLS[g].items():
            im = fitted(cutout(raw))
            im.save(f'{out}/{name}.webp', quality=90, method=6)
            if g == 'run' and name == 'wild':
                for n in (2, 3):
                    badge(im.copy(), f'×{n}').save(f'{out}/wild{n}.webp', quality=90, method=6)
        logo = cutout(LOGOS[g])
        k = min(1000 / logo.width, 560 / logo.height)
        logo = logo.resize((round(logo.width * k), round(logo.height * k)), Image.LANCZOS)
        logo.save(f'{PUB}/{game}/logo.webp', quality=92, method=6)
        for side, size in [('landscape', (1920, 1080)), ('portrait', (1080, 1920))]:
            bg = Image.open(f'{RAW}/{g}-bg-{side}.png').convert('RGB')
            if f'{g}-bg-{side}' in BG_BARS:  # letterbox bars in the render: cut them, then trim the width back to 16:9
                t, b = BG_BARS[f'{g}-bg-{side}']
                bg = bg.crop((0, t, bg.width, bg.height - b))
                w = round(bg.height * size[0] / size[1]); x = (bg.width - w) // 2
                bg = bg.crop((x, 0, x + w, bg.height))
            bg.resize(size, Image.LANCZOS).save(f'{PUB}/{game}/bg-{side}.jpg', quality=85)
        print(game, len(os.listdir(out)), 'symbols')

    # Kimberley Hold's hold cells: an empty dark slot (as Highveld Hold's), in the mine's warm lamplight colours.
    blank = Image.new('RGBA', (SIZE, SIZE))
    d = ImageDraw.Draw(blank)
    m = round(60 * SIZE / 768)
    d.rounded_rectangle((m, m, SIZE - m, SIZE - m), radius=round(90 * SIZE / 768), fill=(48, 28, 12, 150), outline=(230, 170, 80, 170), width=round(8 * SIZE / 768))
    blank.filter(ImageFilter.GaussianBlur(1)).save(f'{PUB}/kimberley-hold/symbols/blank.webp', quality=90, method=6)
