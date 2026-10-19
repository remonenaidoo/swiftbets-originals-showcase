"""Turns the six games' renders into game art (Sunburst Wheel, Coin Flip, Lucky Scratch, Gem Hand, Baccarat, Higher or Lower).
Run with the rembg venv from this folder: ~/.venvs/rembg/bin/python process.py"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from rembg import new_session, remove

RAW, PUB = 'raw', '../../public/assets'
session = new_session('isnet-general-use')

def save(im, game, out):
    os.makedirs(f'{PUB}/{game}', exist_ok=True)
    im.save(f'{PUB}/{game}/{out}.webp', quality=92, method=6)

def cutout(name):
    im = remove(Image.open(f'{RAW}/{name}.png').convert('RGB'), session=session)
    a = np.array(im.getchannel('A'))
    return im.crop(Image.fromarray((a > 60).astype(np.uint8) * 255).getbbox())

def fit(im, w, h):
    """Scale (up or down) to fit w×h, centred on a transparent canvas."""
    k = min(w / im.width, h / im.height)
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
    c = Image.new('RGBA', (w, h))
    c.alpha_composite(im, ((w - im.width) // 2, (h - im.height) // 2))
    return c

def mask(im, shape, radius=0, ss=4):
    """Replaces the alpha with a clean anti-aliased circle / rounded rect (no fringes on round sprites)."""
    w, h = im.size
    m = Image.new('L', (w * ss, h * ss))
    d = ImageDraw.Draw(m)
    if shape == 'circle':
        d.ellipse((ss, ss, w * ss - ss, h * ss - ss), fill=255)
    else:
        d.rounded_rectangle((ss, ss, w * ss - ss, h * ss - ss), radius=radius * ss, fill=255)
    im = im.copy(); im.putalpha(m.resize((w, h), Image.LANCZOS))
    return im

def sprite(name, game, out, size):
    save(fit(cutout(name), size, size), game, out)

def solid(name, game, out, size, strip=0, alpha=None, fill=True):
    """A cut-out that keeps the ORIGINAL pixels, fills see-through interiors (isnet makes glassy objects
    half transparent), hardens the alpha (drops faint shadow smudges) and, with strip>0, removes thin
    stray parts (a ring behind a star, flare rays) by a morphological opening of that radius."""
    src = Image.open(f'{RAW}/{name}.png').convert('RGB')
    a = np.array(remove(src, session=session).getchannel('A')).astype(np.float32) if alpha is None else alpha
    a = np.clip((a - 70) * 255 / 130, 0, 255)
    body = Image.fromarray(((a > 128) * 255).astype(np.uint8))
    outside = body.copy()
    ImageDraw.floodfill(outside, (0, 0), 77)  # everything not reached from the corner is inside the object
    filled = Image.fromarray(((np.array(outside) != 77) * 255).astype(np.uint8)) if fill else body
    if strip:
        filled = filled.filter(ImageFilter.MinFilter(strip * 2 + 1)).filter(ImageFilter.MaxFilter(strip * 2 + 1))
    core = np.array(filled.filter(ImageFilter.MinFilter(5))).astype(np.float32) if fill else 0
    keep = np.array(filled.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1))).astype(np.float32)
    a = np.minimum(np.maximum(a, core), keep)
    im = src.convert('RGBA'); im.putalpha(Image.fromarray(a.astype(np.uint8)))
    im = im.crop(Image.fromarray((a > 60).astype(np.uint8) * 255).getbbox())
    save(fit(im, size, size), game, out)

def star_sprite(name, game, out, size, ss=4):
    """A five-point star with a ring behind it: the alpha is an ideal star polygon fitted to the render
    (tips and inner corners read off the polar profile of a heavily opened mask), so the ring is gone."""
    src = Image.open(f'{RAW}/{name}.png').convert('RGB')
    a = np.array(remove(src, session=session).getchannel('A'))
    body = Image.fromarray(((a > 128) * 255).astype(np.uint8))
    from scipy import ndimage
    yy, xx = np.mgrid[-20:21, -20:21]
    core = ndimage.binary_opening(np.array(body) > 0, structure=(xx ** 2 + yy ** 2) <= 400)
    ys, xs = np.nonzero(core); cy, cx = ys.mean(), xs.mean()
    ang = np.arctan2(ys - cy, xs - cx); rad = np.hypot(ys - cy, xs - cx)
    bins = np.floor((ang + np.pi) / (2 * np.pi) * 720).astype(int) % 720
    prof = np.zeros(720); np.maximum.at(prof, bins, rad)
    prof = np.convolve(np.r_[prof[-5:], prof, prof[:5]], np.ones(11) / 11, 'valid')
    tip = int(np.argmax(prof))  # one tip; the other four are 72° apart (refined locally)
    tips = [max(range(tip + k * 144 - 30, tip + k * 144 + 31), key=lambda i: prof[i % 720]) % 720 for k in range(5)]
    tips.sort()
    pts = []
    for k, t in enumerate(tips):
        nxt = tips[(k + 1) % 5] + (720 if k == 4 else 0)
        inner = min(range(t + 1, nxt), key=lambda i: prof[i % 720])
        for i, r in ((t, prof[t % 720] + 26), (inner, prof[inner % 720])):
            th = i / 720 * 2 * np.pi - np.pi
            pts.append(((cx + np.cos(th) * r) * ss, (cy + np.sin(th) * r) * ss))
    m = Image.new('L', (src.width * ss, src.height * ss)); ImageDraw.Draw(m).polygon(pts, fill=255)
    m = m.resize(src.size, Image.LANCZOS)
    m = Image.fromarray(np.minimum(np.array(m), np.array(body.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1)))))
    im = src.convert('RGBA'); im.putalpha(m)
    save(fit(im.crop(m.point(lambda v: 255 if v > 60 else 0).getbbox()), size, size), game, out)

def round_sprite(name, game, out, size=512):
    """A round object: rembg only finds its bbox; the original pixels are kept and masked to a true circle
    (rembg zeroes colour wherever it cuts, which punched dark holes into the coin's flat field)."""
    src = Image.open(f'{RAW}/{name}.png').convert('RGBA')
    a = np.array(remove(src.convert('RGB'), session=session).getchannel('A'))
    im = src.crop(Image.fromarray((a > 60).astype(np.uint8) * 255).getbbox())
    im = im.resize((size + 4, size + 4), Image.LANCZOS).crop((2, 2, size + 2, size + 2))  # a hair inside the rim
    save(mask(im, 'circle'), game, out)

def silver(im):
    """Turns any gold flecks into silver (keeps the navy)."""
    hsv = np.array(im.convert('RGB').convert('HSV')).astype(np.int32)
    gold = (hsv[..., 0] > 10) & (hsv[..., 0] < 50)
    hsv[..., 1] = np.where(gold, hsv[..., 1] // 10, hsv[..., 1])
    out = Image.fromarray(hsv.astype(np.uint8), 'HSV').convert('RGB').convert('RGBA')
    out.putalpha(im.getchannel('A'))
    return out

def card(name, game, recolour=None):
    """Rendered on white: the card is the bbox of the non-white pixels, kept whole (original pixels; rembg
    zeroes colour inside flat fields) and masked to a clean rounded rectangle."""
    src = Image.open(f'{RAW}/{name}.png').convert('RGB')
    ink = (np.array(src.convert('L')) < 235).astype(np.uint8) * 255
    x0, y0, x1, y1 = Image.fromarray(ink).getbbox()
    im = src.crop((x0 + 3, y0 + 3, x1 - 3, y1 - 3)).convert('RGBA')  # a hair inside the edge (no white rim)
    if recolour: im = recolour(im)
    w, h = 512, round(512 * im.height / im.width)
    im = im.resize((w, h), Image.LANCZOS)
    save(mask(im, 'rect', radius=34), game, 'card-back')

def scratch_frame(name, game):
    """The frame cut out, its plain black inner window made transparent."""
    src = Image.open(f'{RAW}/{name}.png').convert('RGB')
    im = remove(src, session=session)
    dark = (np.array(src.convert('L')) < 28).astype(np.uint8) * 255
    m = Image.fromarray(dark)
    ImageDraw.floodfill(m, (src.width // 2, src.height // 2), 128)
    hole = Image.fromarray(((np.array(m) == 128) * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
    a = np.clip((np.array(im.getchannel('A')).astype(np.float32) - 70) * 255 / 130, 0, 255)
    a = np.minimum(a, 255 - np.array(hole).astype(np.float32))
    lum = np.array(src.convert('L')).astype(np.float32)
    a = a * np.clip((lum - 18) / 24, 0, 1)  # drop the leftover black specks between the scrolls
    im = src.convert('RGBA')
    im.putalpha(Image.fromarray(a.astype(np.uint8)))
    im = im.crop(Image.fromarray((a > 60).astype(np.uint8) * 255).getbbox())
    save(fit(im, 1024, 1024), game, 'card')

def cover_crop(im, w, h):
    k = max(w / im.width, h / im.height)
    im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    x, y = (im.width - w) // 2, (im.height - h) // 2
    return im.crop((x, y, x + w, y + h))

def to_blue(im):
    """Pulls violet/magenta neon round to the table's blue (Higher or Lower is navy + silver): red is drawn
    towards green wherever it exceeds it, a continuous mix with no hue threshold (so no blotchy edges)."""
    x = np.array(im).astype(np.float32)
    r, g = x[..., 0], x[..., 1]
    x[..., 0] = np.where(r > g, g + (r - g) * 0.2, r)
    return Image.fromarray(np.clip(x, 0, 255).astype(np.uint8))

def soften_tray(im):
    """Baccarat: blurs the dim dealer-tray shape at the top centre so it can never read as glyphs."""
    box = (720, 0, 1200, 96)
    m = Image.new('L', im.size); ImageDraw.Draw(m).rectangle(box, fill=255)
    m = m.filter(ImageFilter.GaussianBlur(18))
    return Image.composite(im.filter(ImageFilter.GaussianBlur(26)), im, m)

def backgrounds(game, land_fx=None, port_fx=None):
    os.makedirs(f'{PUB}/{game}', exist_ok=True)
    land = cover_crop(Image.open(f'{RAW}/{game}-bg-landscape.png').convert('RGB'), 1920, 1080)
    port = cover_crop(Image.open(f'{RAW}/{game}-bg-portrait.png').convert('RGB'), 1080, 1920)
    if land_fx: land = land_fx(land)
    if port_fx: port = port_fx(port)
    land.save(f'{PUB}/{game}/bg-landscape.jpg', quality=85)
    port.save(f'{PUB}/{game}/bg-portrait.jpg', quality=85)

if __name__ == '__main__':
    round_sprite('sun-hub-s52', 'sunburst-wheel', 'hub')
    sprite('sun-pointer', 'sunburst-wheel', 'pointer', 512)
    round_sprite('coin-heads-s23', 'coin-flip', 'heads')
    round_sprite('coin-tails-s23', 'coin-flip', 'tails')
    for i, strip, fill in [(1, 0, True), (2, 0, False), (3, 0, False), (5, 0, False), (6, 12, True)]:
        solid(f'scratch-p{i}', 'lucky-scratch', f'p{i}', 384, strip, fill=fill)
    star_sprite('scratch-p4', 'lucky-scratch', 'p4', 384)
    # scratch_frame('scratch-card', 'lucky-scratch')  # skipped: no clean cut (dark enamel vs black bg); see README
    for i in range(1, 8):
        solid('gem-2-s3' if i == 2 else f'gem-{i}', 'gem-hand', f'gem-{i}', 384)
    card('bac-back', 'baccarat')
    card('hl-back-s5', 'higher-lower', silver)
    for g in ['sunburst-wheel', 'coin-flip', 'lucky-scratch', 'gem-hand', 'baccarat', 'higher-lower']:
        if g == 'higher-lower':
            backgrounds(g, to_blue, lambda im: silver(im.convert('RGBA')).convert('RGB'))
        elif g == 'baccarat':
            backgrounds(g, soften_tray)
        else:
            backgrounds(g)
        print(g, sorted(os.listdir(f'{PUB}/{g}')))
