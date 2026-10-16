"""Turns the chosen raw renders into game assets: backgrounds removed (rembg, isnet-general-use), symbols fitted to
384x384, doubles and triples composed from the single so they match exactly, logo, backgrounds and lobby cover.
Run with the rembg venv: ~/.venvs/rembg/bin/python process.py"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from rembg import new_session, remove

RAW, OUT = 'raw', '../../public/assets/gold-reef-rush'
CHOSEN = {'goldbar': 'goldbar29/goldbar', 'diamond': 'diamond', 'cart': 'cart', 'lantern': 'lantern', 'copper': 'copper',
          'silver': 'silver29/silver', 'quartz': 'quartz', 'miner': 'miner29/miner', 'dynamite': 'dynamite'}
LOGO = 'l83/logo'
FONT = '/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf'
SIZE = 384
session = new_session('isnet-general-use')
os.makedirs(f'{OUT}/symbols', exist_ok=True)

def cutout(path):
    im = remove(Image.open(f'{RAW}/{path}.png').convert('RGB'), session=session)
    return im.crop(im.getbbox())

def fit(im, box):
    im = im.copy(); im.thumbnail((box, box), Image.LANCZOS); return im

def place(canvas, im, cx, cy):
    canvas.alpha_composite(im, (int(cx - im.width / 2), int(cy - im.height / 2)))

def shadowed(im):
    """A soft dark halo behind a copy, so stacked copies read as separate objects."""
    pad = 12
    out = Image.new('RGBA', (im.width + pad * 2, im.height + pad * 2))
    a = im.getchannel('A').point(lambda v: 170 if v > 20 else 0)
    halo = Image.new('RGBA', out.size); halo.putalpha(Image.new('L', out.size)); halo.paste((0, 0, 0, 255), (pad, pad), a)
    out.alpha_composite(halo.filter(ImageFilter.GaussianBlur(8)))
    out.alpha_composite(im, (pad, pad)); return out

def badge(canvas, text):
    d = ImageDraw.Draw(canvas)
    f = ImageFont.truetype(FONT, 58)
    w = d.textlength(text, font=f) + 44; h = 74
    x0, y0 = SIZE - w - 10, SIZE - h - 10
    d.rounded_rectangle((x0, y0, x0 + w, y0 + h), radius=h / 2, fill=(40, 14, 8, 235), outline=(255, 205, 90, 255), width=5)
    d.text((x0 + w / 2, y0 + h / 2 + 2), text, font=f, fill=(255, 214, 102, 255), anchor='mm', stroke_width=2, stroke_fill=(90, 40, 0, 255))

def save(im, name):
    im.save(f'{OUT}/symbols/{name}.webp', quality=90, method=6)

def single(im, scale=0.86):
    c = Image.new('RGBA', (SIZE, SIZE)); place(c, fit(im, int(SIZE * scale)), SIZE / 2, SIZE / 2); return c

def multiple(im, n):
    c = Image.new('RGBA', (SIZE, SIZE))
    s = shadowed(fit(im, int(SIZE * (0.66 if n == 2 else 0.62))))
    spots = [(0.36, 0.36), (0.62, 0.6)] if n == 2 else [(0.5, 0.32), (0.32, 0.6), (0.66, 0.62)]
    for x, y in spots: place(c, s, SIZE * x, SIZE * y)
    badge(c, f'×{n}'); return c

art = {k: cutout(v) for k, v in CHOSEN.items()}
for k, name in [('goldbar', 'goldbar'), ('diamond', 'diamond'), ('cart', 'cart'), ('lantern', 'lantern'), ('copper', 'copper'),
                ('silver', 'silver'), ('quartz', 'quartz'), ('miner', 'miner'), ('dynamite', 'dynamite')]:
    save(single(art[k], 1.0 if k == 'dynamite' else 0.86), name)
for k in ['goldbar', 'diamond', 'cart', 'lantern', 'miner']:
    save(multiple(art[k], 2), f'{k}2')
save(multiple(art['goldbar'], 3), 'goldbar3')

logo = cutout(LOGO); logo = fit(logo, 1000); logo.save(f'{OUT}/logo.webp', quality=92, method=6)

def cover_crop(path, w, h):
    im = Image.open(f'{RAW}/{path}.png').convert('RGB'); r = max(w / im.width, h / im.height)
    im = im.resize((round(im.width * r), round(im.height * r)), Image.LANCZOS)
    x, y = (im.width - w) // 2, (im.height - h) // 2; return im.crop((x, y, x + w, y + h))

cover_crop('bg-landscape', 1920, 1080).save(f'{OUT}/bg-landscape.jpg', quality=86)
cover_crop('bg-portrait', 1080, 1920).save(f'{OUT}/bg-portrait.jpg', quality=86)
cov = cover_crop('cover', 600, 600).convert('RGBA')
shade = Image.new('RGBA', cov.size)
for y in range(600): ImageDraw.Draw(shade).line([(0, y), (600, y)], fill=(0, 0, 0, int(max(0, (y - 400) / 200) * 210)))
cov.alpha_composite(shade)
l = fit(logo, 400); cov.alpha_composite(l, ((600 - l.width) // 2, 590 - l.height))
cov.convert('RGB').save(f'{OUT}/cover.jpg', quality=88)
print('done')
