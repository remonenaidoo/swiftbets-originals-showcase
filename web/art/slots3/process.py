"""The three slots' renders into game art. Run with the rembg venv: ~/.venvs/rembg/bin/python process.py
Symbols are cut out and fitted to 768 px; Cape Point Ways' ×2 and ×3 Wilds get a badge; Highveld Hold gets a blank
respin tile; logos are cut out; backgrounds are resized for landscape and portrait."""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from rembg import new_session, remove

RAW, PUB, SIZE = 'raw', '../../public/assets', 768
FONT = '/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf'
GAMES = {'ways': 'cape-point-ways', 'hold': 'highveld-hold', 'gems': 'kalahari-gems'}
SYMBOLS = {
    'ways': {'shark': 'Shark', 'whale': 'Whale', 'lighthouse': 'Lighthouse', 'penguin': 'Penguin', 'anchor': 'Anchor', 'compass': 'Compass', 'shell': 'Shell', 'starfish': 'Starfish', 'wild': 'Wild', 'pearl': 'Pearl'},
    'hold': {'thunderbird': 'Thunderbird', 'windpomp': 'Windpomp', 'springbok': 'Springbok', 'cosmos': 'Cosmos', 'ruby': 'Ruby', 'sapphire': 'Sapphire', 'emerald': 'Emerald', 'amber': 'Amber', 'wild': 'Wild', 'coin': 'Coin'},
    'gems': {'sunstone': 'Sunstone', 'tanzanite': 'Tanzanite', 'tigerseye': 'TigersEye', 'desertrose': 'DesertRose', 'quartz': 'Quartz', 'agate': 'Agate', 'jasper': 'Jasper', 'amethyst': 'Amethyst', 'opal': 'Opal', 'sun': 'Sun', 'meteor': 'Meteor'},
}
LOGOS = {'ways': 'ways-logo-11', 'hold': 'hold-logo', 'gems': 'gems-logo-11'}
session = new_session('isnet-general-use')

# Renders with a sparkle halo are cropped to their solid pixels, or the halo shrinks the symbol in its cell.
SOLID = {'gems-tanzanite': 220}

def cutout(name):
    im = remove(Image.open(f'{RAW}/{name}.png').convert('RGB'), session=session)
    a = np.array(im.getchannel('A'))
    return im.crop(Image.fromarray((a > SOLID.get(name, 60)).astype(np.uint8) * 255).getbbox())

def glow_logo(name):
    """A logo rendered on black with see-through lettering (water): alpha from brightness, so nothing is cut away."""
    im = np.array(Image.open(f'{RAW}/{name}.png').convert('RGB')).astype(np.float32)
    a = np.clip((im.max(axis=2) - 10) * 4, 0, 255)
    out = Image.fromarray(np.dstack([im, a]).astype(np.uint8), 'RGBA')
    return out.crop(Image.fromarray((a > 40).astype(np.uint8) * 255).getbbox())

def fitted(im, scale=0.94):
    # Scaled to fit the box either way (thumbnail would never enlarge a small cut-out).
    k = SIZE * scale / max(im.width, im.height)
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
    c = Image.new('RGBA', (SIZE, SIZE)); c.alpha_composite(im, ((SIZE - im.width) // 2, (SIZE - im.height) // 2)); return c

def badge(c, text, fill=(10, 40, 60, 235), ring=(120, 240, 255, 255), ink=(200, 250, 255, 255)):
    d = ImageDraw.Draw(c)
    f = ImageFont.truetype(FONT, 116)
    w = d.textlength(text, font=f) + 88; h = 148
    x0, y0 = SIZE - w - 20, SIZE - h - 20
    d.rounded_rectangle((x0, y0, x0 + w, y0 + h), radius=h / 2, fill=fill, outline=ring, width=10)
    d.text((x0 + w / 2, y0 + h / 2 + 4), text, font=f, fill=ink, anchor='mm', stroke_width=4, stroke_fill=(0, 30, 50, 255))
    return c

for g, game in GAMES.items():
    out = f'{PUB}/{game}/symbols'
    os.makedirs(out, exist_ok=True)
    for raw, name in SYMBOLS[g].items():
        im = fitted(cutout(f'{g}-{raw}'))
        im.save(f'{out}/{name.lower()}.webp', quality=90, method=6)
        if g == 'ways' and raw == 'wild':
            for n in (2, 3):
                badge(im.copy(), f'×{n}').save(f'{out}/wild{n}.webp', quality=90, method=6)
    logo = cutout(LOGOS[g]) if g != 'ways' else glow_logo(LOGOS[g])
    logo.thumbnail((1000, 560), Image.LANCZOS); logo.save(f'{PUB}/{game}/logo.webp', quality=92, method=6)
    for side, size in [('landscape', (1920, 1080)), ('portrait', (1080, 1920))]:
        Image.open(f'{RAW}/{g}-bg-{side}.png').convert('RGB').resize(size, Image.LANCZOS).save(f'{PUB}/{game}/bg-{side}.jpg', quality=85)
    print(game, len(os.listdir(out)), 'symbols')

# Highveld Hold's respin cells: an empty dark slot, so coins stand out during the hold.
blank = Image.new('RGBA', (SIZE, SIZE))
d = ImageDraw.Draw(blank)
d.rounded_rectangle((60, 60, SIZE - 60, SIZE - 60), radius=90, fill=(30, 18, 60, 150), outline=(170, 130, 255, 170), width=8)
blank.filter(ImageFilter.GaussianBlur(1)).save(f'{PUB}/highveld-hold/symbols/blank.webp', quality=90, method=6)
