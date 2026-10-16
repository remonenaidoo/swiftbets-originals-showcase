"""Lobby covers for the instant games, drawn in code (no generated art) until the artist's set arrives.
Run: python3 covers.py  →  ../../public/assets/{crash,mines}/cover.jpg (600×600)."""
import math, os, random
from PIL import Image, ImageDraw, ImageFilter, ImageFont

S, OUT = 600, '../../public/assets'
BOLD = '/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf'
random.seed(7)

def gradient(top, bottom):
    im = Image.new('RGB', (S, S))
    d = ImageDraw.Draw(im)
    for y in range(S):
        k = y / (S - 1)
        d.line([(0, y), (S, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * k) for i in range(3)))
    return im

def title(im, text, colour, y):
    band = Image.new('RGBA', im.size)
    ImageDraw.Draw(band).rectangle((0, y - 90, S, S), fill=(0, 0, 0, 150))
    band = band.filter(ImageFilter.GaussianBlur(40))
    im.paste(band, (0, 0), band)
    d = ImageDraw.Draw(im)
    f = ImageFont.truetype(BOLD, 112)
    glow = Image.new('RGBA', im.size)
    ImageDraw.Draw(glow).text((S / 2, y), text, font=f, anchor='mm', fill=colour + (255,))
    im.paste(glow.filter(ImageFilter.GaussianBlur(14)), (0, 0), glow.filter(ImageFilter.GaussianBlur(14)))
    d.text((S / 2, y), text, font=f, anchor='mm', fill=(255, 255, 255), stroke_width=4, stroke_fill=colour)
    d.text((S / 2, y + 66), 'SWIFTBETS ORIGINALS', font=ImageFont.truetype(BOLD, 22), anchor='mm', fill=(210, 220, 240))

def crash():
    im = gradient((14, 18, 60), (3, 4, 12))
    d = ImageDraw.Draw(im)
    for _ in range(160):
        x, y, r = random.random() * S, random.random() * S, random.choice([1, 1, 2])
        d.ellipse((x - r, y - r, x + r, y + r), fill=(255, 255, 255))
    pts = [(40 + t * 4.6, 400 - (math.exp(t * 0.034) - 1) * 9.6) for t in range(0, 104)]
    layer = Image.new('RGBA', im.size)
    ld = ImageDraw.Draw(layer)
    ld.polygon(pts + [(pts[-1][0], 420), (40, 420)], fill=(140, 40, 160, 90))
    ld.line(pts, fill=(255, 111, 216, 255), width=12, joint='curve')
    im.paste(layer.filter(ImageFilter.GaussianBlur(10)), (0, 0), layer.filter(ImageFilter.GaussianBlur(10)))
    d.line(pts, fill=(255, 150, 230), width=6, joint='curve')
    x, y = pts[-1]
    rocket = Image.new('RGBA', (200, 120))
    rd = ImageDraw.Draw(rocket)
    rd.polygon([(20, 50), (-10, 60), (20, 70)], fill=(255, 180, 70))
    rd.ellipse((30, 40, 150, 80), fill=(235, 240, 250))
    rd.polygon([(140, 44), (186, 60), (140, 76)], fill=(255, 77, 109))
    rd.ellipse((96, 50, 116, 70), fill=(90, 209, 255), outline=(36, 48, 79), width=3)
    rd.polygon([(50, 42), (26, 14), (80, 42)], fill=(255, 77, 109))
    rd.polygon([(50, 78), (26, 106), (80, 78)], fill=(255, 77, 109))
    rd.polygon([(30, 52), (0, 60), (30, 68)], fill=(255, 241, 168))
    rocket = rocket.rotate(42, expand=True, resample=Image.BICUBIC)
    im.paste(rocket, (int(x - rocket.width / 2), int(y - rocket.height / 2)), rocket)
    title(im, 'CRASH', (255, 111, 216), 478)
    return im

def gem(d, cx, cy, w):
    h, hw, tw = w * 0.86, w / 2, w * 0.28
    top, gir, tip = cy - h * 0.42, cy - h * 0.08, cy + h * 0.5
    d.polygon([(cx - hw, gir), (cx - tw, top), (cx + tw, top), (cx + hw, gir)], fill=(92, 242, 192))
    d.polygon([(cx - tw, top), (cx, gir), (cx + tw, top)], fill=(157, 255, 224))
    d.polygon([(cx - hw, gir), (cx, tip), (cx, gir)], fill=(31, 165, 122))
    d.polygon([(cx + hw, gir), (cx, tip), (cx, gir)], fill=(19, 138, 99))
    d.polygon([(cx - hw * .45, gir), (cx, tip), (cx + hw * .45, gir)], fill=(70, 230, 176))

def mines():
    im = gradient((12, 34, 53), (5, 11, 20))
    d = ImageDraw.Draw(im)
    size, gap = 74, 8
    x0 = (S - (size * 5 + gap * 4)) / 2
    layout = {6: 'gem', 8: 'gem', 12: 'mine', 16: 'gem', 18: 'gem', 22: 'gem'}
    for i in range(25):
        x, y = x0 + (i % 5) * (size + gap), 24 + (i // 5) * (size + gap)
        face = layout.get(i)
        fill = (15, 58, 51) if face == 'gem' else (58, 15, 24) if face == 'mine' else (36, 64, 94)
        edge = (54, 226, 160) if face == 'gem' else (255, 77, 94) if face == 'mine' else (79, 122, 166)
        d.rounded_rectangle((x, y, x + size, y + size), radius=16, fill=fill, outline=edge, width=3)
        cx, cy = x + size / 2, y + size / 2
        if face == 'gem':
            gem(d, cx, cy, size * 0.62)
        elif face == 'mine':
            r = size * 0.2
            for k in range(8):
                a = k / 8 * math.tau
                d.line([(cx + math.cos(a) * r * .8, cy + math.sin(a) * r * .8), (cx + math.cos(a) * r * 1.45, cy + math.sin(a) * r * 1.45)], fill=(90, 96, 108), width=7)
            d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(35, 38, 46))
            d.ellipse((cx - r * .4, cy - r * .4, cx + r * .4, cy + r * .4), fill=(255, 77, 94))
    title(im, 'MINES', (54, 226, 160), 486)
    return im

A = '../../public/assets'

def crash_art():
    """The game's own art: Cape Town at night, the rocket climbing over it, its trail."""
    im = gradient((13, 18, 64), (43, 26, 94)).convert('RGBA')
    city = Image.open(f'{A}/crash/city.webp')
    k = S * 1.6 / city.width
    city = city.resize((int(city.width * k), int(city.height * k)), Image.LANCZOS)
    im.alpha_composite(city, ((S - city.width) // 2, 452 - city.height))
    ImageDraw.Draw(im).rectangle((0, 452, S, S), fill=(8, 14, 40, 255))
    d = ImageDraw.Draw(im)
    for _ in range(90):
        x, y = random.random() * S, random.random() * 260
        d.ellipse((x - 1, y - 1, x + 1, y + 1), fill=(255, 255, 255, 220))
    trail = Image.new('RGBA', im.size)
    td = ImageDraw.Draw(trail)
    pts = [(30 + t * 3.8, 430 - (math.exp(t * 0.03) - 1) * 11.5) for t in range(0, 100)]
    td.line(pts, fill=(255, 111, 216, 255), width=16, joint='curve')
    im.alpha_composite(trail.filter(ImageFilter.GaussianBlur(9)))
    ImageDraw.Draw(im).line(pts, fill=(255, 205, 240, 255), width=5, joint='curve')
    rocket = Image.open(f'{A}/crash/rocket.webp')
    rocket.thumbnail((250, 250), Image.LANCZOS)
    x, y = pts[-1]
    im.alpha_composite(rocket, (int(x - rocket.width * 0.12), int(y - rocket.height * 0.86)))
    im = im.convert('RGB')
    title(im, 'CRASH', (255, 111, 216), 486)
    return im

def mines_art():
    """The Big Hole at night, a few revealed stones: diamonds and one mine."""
    bg = Image.open(f'{A}/mines/bg-portrait.jpg').convert('RGBA')
    bg = bg.resize((S, int(bg.height * S / bg.width))).crop((0, 120, S, 120 + S))
    im = bg
    tile, gem, mine = (Image.open(f'{A}/mines/{n}.webp') for n in ('tile', 'diamond', 'mine'))
    size, gap = 104, 12
    x0, y0 = (S - (size * 4 + gap * 3)) / 2, 70
    finds = {1: gem, 4: gem, 6: mine, 9: gem, 11: gem, 14: gem}
    for i in range(12):
        x, y = int(x0 + (i % 4) * (size + gap)), int(y0 + (i // 4) * (size + gap))
        t = tile.resize((size, size), Image.LANCZOS)
        im.alpha_composite(t, (x, y))
        f = finds.get(i)
        if f is not None:
            g = f.resize((int(size * .82), int(size * .82)), Image.LANCZOS)
            im.alpha_composite(g, (x + (size - g.width) // 2, y + (size - g.height) // 2))
    im = im.convert('RGB')
    title(im, 'MINES', (54, 226, 160), 486)
    return im

crash, mines = crash_art, mines_art

for name, make in [('crash', crash), ('mines', mines)]:
    os.makedirs(f'{OUT}/{name}', exist_ok=True)
    make().save(f'{OUT}/{name}/cover.jpg', quality=88)
    print('wrote', name)
