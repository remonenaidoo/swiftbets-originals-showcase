"""Lobby covers for the instant games, drawn in code (no generated art) until the artist's set arrives.
Run: python3 covers.py  →  ../../public/assets/{crash,mines}/cover.jpg (600×600)."""
import math, os, random, sys
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

def slot_cover(game, hero, size=340):
    """A slot's cover: its backdrop, its hero symbol, its logo across the bottom."""
    def make():
        b = Image.open(f'{A}/{game}/bg-portrait.jpg').convert('RGBA')
        b = b.resize((S, int(b.height * S / b.width)), Image.LANCZOS)
        top = max(0, (b.height - S) // 2)
        im = b.crop((0, top, S, top + S))
        im.alpha_composite(Image.new('RGBA', im.size, (0, 0, 0, 70)))
        h = Image.open(f'{A}/{game}/symbols/{hero}.webp'); h.thumbnail((size, size), Image.LANCZOS)
        im.alpha_composite(h, ((S - h.width) // 2, 70))
        band = Image.new('RGBA', im.size); ImageDraw.Draw(band).rectangle((0, 380, S, S), fill=(0, 0, 0, 150))
        im.alpha_composite(band.filter(ImageFilter.GaussianBlur(30)))
        logo = Image.open(f'{A}/{game}/logo.webp'); logo.thumbnail((540, 200), Image.LANCZOS)
        im.alpha_composite(logo, ((S - logo.width) // 2, S - logo.height - 22))
        return im.convert('RGB')
    return make

def hero(game, sprite, title_text, colour, size=330, at=(0.5, 0.42), bg='bg-portrait.jpg', darken=0, pegs=False, layers=False):
    """A cover from a game's backdrop and its key sprite, title at the bottom."""
    def make():
        b = Image.open(f'{A}/{game}/{bg}').convert('RGBA')
        b = b.resize((S, int(b.height * S / b.width)), Image.LANCZOS)
        top = max(0, (b.height - S) // 2 - 60)
        im = b.crop((0, top, S, top + S))
        if darken:
            im.alpha_composite(Image.new('RGBA', im.size, (4, 2, 14, darken)))
        if pegs:
            d = ImageDraw.Draw(im)
            for r in range(8):
                for j in range(r + 3):
                    x, y = S / 2 + (j - (r + 2) / 2) * 44, 200 + r * 36
                    d.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(255, 224, 176))
            cols = [(255, 61, 110), (255, 122, 61), (255, 179, 71), (255, 217, 107), (255, 217, 107), (255, 179, 71), (255, 122, 61), (255, 61, 110)]
            for k, c in enumerate(cols + [cols[0]]):
                x = S / 2 + (k - 4) * 44
                d.rounded_rectangle((x - 19, 470 - 60, x + 19, 470 - 34), radius=6, fill=c)
        sp = Image.open(f'{A}/{game}/{sprite}')
        sp.thumbnail((size, size), Image.LANCZOS)
        glow = Image.new('RGBA', im.size)
        if not layers:
            g = Image.new('RGBA', sp.size, colour + (200,))
            g.putalpha(sp.getchannel('A'))
            glow.alpha_composite(g, (int(S * at[0] - sp.width / 2), int(S * at[1] - sp.height / 2)))
        if not layers:
            im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(28)))
        if layers:  # light on black (a comet): added, not pasted
            from PIL import ImageChops
            add = Image.new('RGB', im.size)
            add.paste(sp.convert('RGB'), (int(S * at[0] - sp.width / 2), int(S * at[1] - sp.height / 2)))
            im = ImageChops.add(im.convert('RGB'), add).convert('RGBA')
        else:
            im.alpha_composite(sp, (int(S * at[0] - sp.width / 2), int(S * at[1] - sp.height / 2)))
        im = im.convert('RGB')
        title(im, title_text, colour, 486)
        return im
    return make

def keno_art():
    im = Image.open(f'{A}/keno/bg-landscape.jpg').convert('RGBA')
    im = im.resize((int(im.width * S / im.height), S), Image.LANCZOS)
    im = im.crop(((im.width - S) // 2, 0, (im.width + S) // 2, S))
    ball = Image.open(f'{A}/keno/ball.webp')
    d = ImageDraw.Draw(im)
    f = ImageFont.truetype(BOLD, 44)
    for i, (n, x, y, c) in enumerate([(7, 130, 150, (54, 226, 160)), (21, 300, 110, (59, 109, 255)), (34, 470, 160, (255, 210, 63)), (12, 215, 290, (255, 77, 109)), (40, 390, 290, (54, 226, 160))]):
        b = ball.resize((150, 150), Image.LANCZOS)
        im.alpha_composite(b, (x - 75, y - 75))
        d.ellipse((x - 42, y - 42, x + 42, y + 42), fill=c)
        d.text((x, y), str(n), font=f, anchor='mm', fill=(255, 255, 255))
    im = im.convert('RGB')
    title(im, 'KENO', (255, 210, 63), 486)
    return im


# --- The six (Sunburst Wheel, Coin Flip, Lucky Scratch, Gem Hand, Baccarat, Higher or Lower): art in ../six ---

def title_fit(im, text, colour, y, size=112):
    """title() with the font shrunk until a long name fits the width."""
    while size > 40 and ImageFont.truetype(BOLD, size).getlength(text) > S - 56:
        size -= 2
    band = Image.new('RGBA', im.size)
    ImageDraw.Draw(band).rectangle((0, y - 90, S, S), fill=(0, 0, 0, 150))
    band = band.filter(ImageFilter.GaussianBlur(40))
    im.paste(band, (0, 0), band)
    d = ImageDraw.Draw(im)
    f = ImageFont.truetype(BOLD, size)
    glow = Image.new('RGBA', im.size)
    ImageDraw.Draw(glow).text((S / 2, y), text, font=f, anchor='mm', fill=colour + (255,))
    im.paste(glow.filter(ImageFilter.GaussianBlur(14)), (0, 0), glow.filter(ImageFilter.GaussianBlur(14)))
    d.text((S / 2, y), text, font=f, anchor='mm', fill=(255, 255, 255), stroke_width=max(3, size // 28), stroke_fill=colour)
    d.text((S / 2, y + 66), 'SWIFTBETS ORIGINALS', font=ImageFont.truetype(BOLD, 22), anchor='mm', fill=(210, 220, 240))

def backdrop(game, shift=60, darken=0, bg='bg-portrait.jpg'):
    b = Image.open(f'{A}/{game}/{bg}').convert('RGBA')
    if b.width >= b.height:
        b = b.resize((int(b.width * S / b.height), S), Image.LANCZOS)
        x = (b.width - S) // 2
        im = b.crop((x, 0, x + S, S))
    else:
        b = b.resize((S, int(b.height * S / b.width)), Image.LANCZOS)
        top = max(0, (b.height - S) // 2 - shift)
        im = b.crop((0, top, S, top + S))
    if darken:
        im.alpha_composite(Image.new('RGBA', im.size, (0, 0, 0, darken)))
    return im

def sprite_at(im, path, size, cx, cy, rotate=0, glow=None, squash=1.0):
    sp = Image.open(path).convert('RGBA')
    k = size / max(sp.size)
    sp = sp.resize((max(1, int(sp.width * k * squash)), max(1, int(sp.height * k))), Image.LANCZOS)
    if rotate:
        sp = sp.rotate(rotate, expand=True, resample=Image.BICUBIC)
    x, y = int(cx - sp.width / 2), int(cy - sp.height / 2)
    if glow:
        g = Image.new('RGBA', sp.size, glow + (190,)); g.putalpha(sp.getchannel('A'))
        layer = Image.new('RGBA', im.size); layer.paste(g, (x, y), g)
        im.alpha_composite(layer.filter(ImageFilter.GaussianBlur(24)))
    shadow = Image.new('RGBA', sp.size, (0, 0, 0, 150)); shadow.putalpha(sp.getchannel('A').point(lambda v: v * 0.55))
    sh = Image.new('RGBA', im.size); sh.paste(shadow, (x + 8, y + 12), shadow)
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(10)))
    tmp = Image.new('RGBA', im.size); tmp.paste(sp, (x, y), sp)
    im.alpha_composite(tmp)

def sunburst_wheel_art():
    im = backdrop('sunburst-wheel', shift=40, darken=40)
    cx, cy, r = S / 2, 250, 178
    wheel = Image.new('RGBA', (S * 2, S * 2)); d = ImageDraw.Draw(wheel)
    cols = [(255, 194, 77), (124, 58, 237), (255, 140, 66), (88, 40, 170), (255, 214, 120), (150, 70, 230)]
    c2, r2 = (cx * 2, cy * 2), r * 2
    d.ellipse((c2[0] - r2 - 18, c2[1] - r2 - 18, c2[0] + r2 + 18, c2[1] + r2 + 18), fill=(120, 72, 20))
    d.ellipse((c2[0] - r2 - 10, c2[1] - r2 - 10, c2[0] + r2 + 10, c2[1] + r2 + 10), fill=(255, 206, 110))
    for k in range(12):
        d.pieslice((c2[0] - r2, c2[1] - r2, c2[0] + r2, c2[1] + r2), k * 30 - 90 - 15, k * 30 - 75, fill=cols[k % 6])
    for k in range(12):
        a = math.radians(k * 30 - 90 - 15)
        d.line([c2, (c2[0] + math.cos(a) * r2, c2[1] + math.sin(a) * r2)], fill=(255, 230, 170), width=5)
        bx, by = c2[0] + math.cos(a) * (r2 + 2), c2[1] + math.sin(a) * (r2 + 2)
        d.ellipse((bx - 9, by - 9, bx + 9, by + 9), fill=(255, 248, 220))
    wheel = wheel.resize(im.size, Image.LANCZOS)
    glow = Image.new('RGBA', im.size); ImageDraw.Draw(glow).ellipse((cx - r - 30, cy - r - 30, cx + r + 30, cy + r + 30), fill=(255, 180, 60, 120))
    im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(30)))
    im.alpha_composite(wheel)
    sprite_at(im, f'{A}/sunburst-wheel/hub.webp', 112, cx, cy)
    sprite_at(im, f'{A}/sunburst-wheel/pointer.webp', 78, cx, cy - r - 4)
    im = im.convert('RGB')
    title_fit(im, 'SUNBURST WHEEL', (255, 194, 77), 486)
    return im

def coin_flip_art():
    im = backdrop('coin-flip', shift=0, bg='bg-landscape.jpg')
    sprite_at(im, f'{A}/coin-flip/tails.webp', 210, 405, 175, rotate=-18, squash=0.42)
    sprite_at(im, f'{A}/coin-flip/heads.webp', 300, 255, 245, glow=(255, 200, 90))
    im = im.convert('RGB')
    title_fit(im, 'COIN FLIP', (52, 214, 190), 486)
    return im

def lucky_scratch_art():
    im = backdrop('lucky-scratch', shift=40, bg='bg-landscape.jpg')
    x0, y0, w, h = 110, 40, 380, 360
    card = Image.new('RGBA', (S * 2, S * 2)); d = ImageDraw.Draw(card)
    d.rounded_rectangle((x0 * 2, y0 * 2, (x0 + w) * 2, (y0 + h) * 2), radius=44, fill=(255, 206, 90))
    d.rounded_rectangle((x0 * 2 + 12, y0 * 2 + 12, (x0 + w) * 2 - 12, (y0 + h) * 2 - 12), radius=36, fill=(10, 70, 46))
    card = card.resize(im.size, Image.LANCZOS)
    sh = Image.new('RGBA', im.size); ImageDraw.Draw(sh).rounded_rectangle((x0 + 10, y0 + 16, x0 + w + 10, y0 + h + 16), radius=22, fill=(0, 0, 0, 160))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(14)))
    im.alpha_composite(card)
    cell, gap = 104, 12
    gx, gy = x0 + (w - (cell * 3 + gap * 2)) / 2, y0 + (h - (cell * 3 + gap * 2)) / 2
    revealed = {0: 'p6', 2: 'p5', 4: 'p6', 6: 'p1', 8: 'p6'}
    d = ImageDraw.Draw(im)
    for i in range(9):
        cx0, cy0 = gx + (i % 3) * (cell + gap), gy + (i // 3) * (cell + gap)
        if i in revealed:
            d.rounded_rectangle((cx0, cy0, cx0 + cell, cy0 + cell), radius=14, fill=(18, 96, 64), outline=(255, 214, 120), width=2)
            sprite_at(im, f'{A}/lucky-scratch/{revealed[i]}.webp', cell * 0.78, cx0 + cell / 2, cy0 + cell / 2)
        else:  # silver foil, with a few scratch marks
            foil = Image.new('RGBA', (cell, cell)); fd = ImageDraw.Draw(foil)
            for yy in range(cell):
                v = int(168 + 50 * math.sin(yy / cell * math.pi))
                fd.line([(0, yy), (cell, yy)], fill=(v, v, v + 8, 255))
            m = Image.new('L', (cell, cell)); ImageDraw.Draw(m).rounded_rectangle((0, 0, cell - 1, cell - 1), radius=14, fill=255)
            im.paste(foil, (int(cx0), int(cy0)), m)
            for k in range(3):
                yy = cy0 + 30 + k * 22
                d.line([(cx0 + 18, yy), (cx0 + cell - 22, yy - 10)], fill=(232, 232, 238), width=3)
    im = im.convert('RGB')
    title_fit(im, 'LUCKY SCRATCH', (54, 226, 120), 486)
    return im

def gem_hand_art():
    im = backdrop('gem-hand', shift=0, bg='bg-landscape.jpg', darken=40)
    for k, g in enumerate([1, 3, 5, 6, 7]):
        a = (k - 2) * 0.42
        cx, cy = S / 2 + math.sin(a) * 250, 400 - math.cos(a) * 200
        sprite_at(im, f'{A}/gem-hand/gem-{g}.webp', 128, cx, cy, rotate=-math.degrees(a) * 0.5,
                  glow=[(255, 70, 90), (255, 214, 60), (60, 220, 255), (70, 110, 255), (190, 90, 255)][k])
    im = im.convert('RGB')
    title_fit(im, 'GEM HAND', (230, 90, 210), 486)
    return im

def baccarat_art():
    im = backdrop('baccarat', shift=0, bg='bg-landscape.jpg')
    for cx, cy, rot in [(225, 230, 14), (300, 215, 0), (375, 230, -14)]:
        sprite_at(im, f'{A}/baccarat/card-back.webp', 290, cx, cy, rotate=rot)
    im = im.convert('RGB')
    title_fit(im, 'BACCARAT', (235, 70, 80), 486)
    return im

def higher_lower_art():
    im = backdrop('higher-lower', shift=0, bg='bg-landscape.jpg')
    sprite_at(im, f'{A}/higher-lower/card-back.webp', 300, S / 2, 225, glow=(120, 170, 255))
    d = ImageDraw.Draw(im)
    for side in (-1, 1):  # up / down chevrons either side of the card
        x = S / 2 + side * 175
        up = [(x - 40, 175), (x, 125), (x + 40, 175)]
        dn = [(x - 40, 275), (x, 325), (x + 40, 275)]
        lay = Image.new('RGBA', im.size); ld = ImageDraw.Draw(lay)
        ld.line(up, fill=(80, 230, 150, 255), width=16, joint='curve'); ld.line(dn, fill=(255, 90, 110, 255), width=16, joint='curve')
        im.alpha_composite(lay.filter(ImageFilter.GaussianBlur(8)))
        d.line(up, fill=(200, 255, 225), width=8, joint='curve'); d.line(dn, fill=(255, 210, 215), width=8, joint='curve')
    im = im.convert('RGB')
    title_fit(im, 'HIGHER OR LOWER', (120, 170, 255), 486)
    return im

for name, make in [('crash', crash), ('mines', mines),
                   ('dice', hero('dice', 'die.webp', 'DICE', (255, 95, 210))),
                   ('limbo', hero('limbo', 'comet.webp', 'LIMBO', (255, 194, 77), 400, (0.5, 0.4), darken=150, layers=True)),
                   ('peg-drop', hero('peg-drop', 'ball.webp', 'PEG DROP', (255, 179, 71), 120, (0.53, 0.2), darken=60, pegs=True)),
                   ('towers', hero('towers', 'snake.webp', 'TOWERS', (255, 194, 77), 300)),
                   ('keno', keno_art),
                   ('cape-point-ways', slot_cover('cape-point-ways', 'shark')),
                   ('highveld-hold', slot_cover('highveld-hold', 'coin', 300)),
                   ('kalahari-gems', slot_cover('kalahari-gems', 'sunstone', 300)),
                   ('sunburst-wheel', sunburst_wheel_art), ('coin-flip', coin_flip_art), ('lucky-scratch', lucky_scratch_art),
                   ('gem-hand', gem_hand_art), ('baccarat', baccarat_art), ('higher-lower', higher_lower_art)]:
    if sys.argv[1:] and name not in sys.argv[1:]:  # optional: python3 covers.py <name> ... renders only those
        continue
    os.makedirs(f'{OUT}/{name}', exist_ok=True)
    make().save(f'{OUT}/{name}/cover.jpg', quality=88)
    print('wrote', name)
