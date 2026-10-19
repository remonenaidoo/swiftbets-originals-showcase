"""Lobby covers for the four new slots, built like the existing slot covers (../instant/covers.py slot_cover):
the game's portrait backdrop, its hero symbol with a soft glow, its logo across the bottom, SWIFTBETS ORIGINALS under it.
Run from this folder (needs Pillow): python3 covers.py  ->  ../../public/assets/<game>/cover.jpg (600x600)."""
from PIL import Image, ImageDraw, ImageFilter, ImageFont

S, A = 600, '../../public/assets'
BOLD = '/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf'


def cover(game, hero, glow, size=330, top=40, shift=0):
    b = Image.open(f'{A}/{game}/bg-portrait.jpg').convert('RGBA')
    b = b.resize((S, int(b.height * S / b.width)), Image.LANCZOS)
    y = max(0, min(b.height - S, (b.height - S) // 2 + shift))
    im = b.crop((0, y, S, y + S))
    im.alpha_composite(Image.new('RGBA', im.size, (0, 0, 0, 60)))
    h = Image.open(f'{A}/{game}/symbols/{hero}.webp').convert('RGBA')
    h = h.crop(h.getchannel('A').getbbox())
    k = size / max(h.size); h = h.resize((round(h.width * k), round(h.height * k)), Image.LANCZOS)
    at = ((S - h.width) // 2, top)
    g = Image.new('RGBA', h.size, glow + (190,)); g.putalpha(h.getchannel('A').point(lambda v: v * 190 // 255))
    layer = Image.new('RGBA', im.size); layer.alpha_composite(g, at)
    im.alpha_composite(layer.filter(ImageFilter.GaussianBlur(26)))
    im.alpha_composite(h, at)
    band = Image.new('RGBA', im.size); ImageDraw.Draw(band).rectangle((0, 370, S, S), fill=(0, 0, 0, 165))
    im.alpha_composite(band.filter(ImageFilter.GaussianBlur(30)))
    logo = Image.open(f'{A}/{game}/logo.webp').convert('RGBA')
    k = min(540 / logo.width, 180 / logo.height); logo = logo.resize((round(logo.width * k), round(logo.height * k)), Image.LANCZOS)
    im.alpha_composite(logo, ((S - logo.width) // 2, S - logo.height - 46))
    d = ImageDraw.Draw(im)
    d.text((S / 2, S - 24), 'SWIFTBETS ORIGINALS', font=ImageFont.truetype(BOLD, 20), anchor='mm', fill=(225, 230, 242, 255),
           stroke_width=2, stroke_fill=(0, 0, 0, 200))
    return im.convert('RGB')


for game, hero, glow, kw in [
    ('karoo-stargazer', 'leopard', (120, 170, 255), {}),
    ('sardine-run', 'dolphin', (90, 230, 255), {'size': 340}),
    ('kimberley-hold', 'prospector', (255, 170, 70), {'size': 320, 'top': 50}),
    ('fynbos-bloom', 'kingprotea', (255, 150, 200), {'size': 340}),
]:
    cover(game, hero, glow, **kw).save(f'{A}/{game}/cover.jpg', quality=88)
    print('wrote', game)
