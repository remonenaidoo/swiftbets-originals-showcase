"""Turns the five games' renders into game art (Dice, Limbo, Peg Drop, Towers, Keno).
Run with the rembg venv: ~/.venvs/rembg/bin/python process.py"""
import os
import numpy as np
from PIL import Image
from rembg import new_session, remove

RAW, PUB = 'raw', '../../public/assets'
session = new_session('isnet-general-use')

def cut(name, game, out, size=512):
    im = remove(Image.open(f'{RAW}/{name}.png').convert('RGB'), session=session)
    a = np.array(im.getchannel('A'))
    im = im.crop(Image.fromarray((a > 60).astype(np.uint8) * 255).getbbox())
    im.thumbnail((size, size), Image.LANCZOS)
    c = Image.new('RGBA', (size, size))
    c.alpha_composite(im, ((size - im.width) // 2, (size - im.height) // 2))
    os.makedirs(f'{PUB}/{game}', exist_ok=True)
    c.save(f'{PUB}/{game}/{out}.webp', quality=92, method=6)

def backgrounds(game, portrait_from_landscape=False):
    os.makedirs(f'{PUB}/{game}', exist_ok=True)
    land = Image.open(f'{RAW}/{game}-bg-landscape.png').convert('RGB')
    land.resize((1920, 1080), Image.LANCZOS).save(f'{PUB}/{game}/bg-landscape.jpg', quality=85)
    if portrait_from_landscape:
        w = int(land.height * 9 / 16)
        port = land.crop(((land.width - w) // 2, 0, (land.width + w) // 2, land.height))
    else:
        port = Image.open(f'{RAW}/{game}-bg-portrait.png').convert('RGB')
    port.resize((1080, 1920), Image.LANCZOS).save(f'{PUB}/{game}/bg-portrait.jpg', quality=85)

cut('dice-die', 'dice', 'die')
# The comet is light on black: kept whole (no cut-out, which leaves dark fringes) and drawn additively.
Image.open(f'{RAW}/limbo-comet.png').convert('RGB').resize((512, 512), Image.LANCZOS).save(f'{PUB}/limbo/comet.webp', quality=92, method=6)
cut('peg-ball', 'peg-drop', 'ball', 256)
cut('towers-fruit', 'towers', 'fruit')
cut('towers-snake', 'towers', 'snake')
cut('towers-plank', 'towers', 'plank')
cut('keno-ball', 'keno', 'ball', 256)
backgrounds('dice'); backgrounds('limbo')
os.rename(f'{RAW}/peg-bg-landscape.png', f'{RAW}/peg-drop-bg-landscape.png') if os.path.exists(f'{RAW}/peg-bg-landscape.png') else None
os.rename(f'{RAW}/peg-bg-portrait.png', f'{RAW}/peg-drop-bg-portrait.png') if os.path.exists(f'{RAW}/peg-bg-portrait.png') else None
backgrounds('peg-drop'); backgrounds('towers'); backgrounds('keno', portrait_from_landscape=True)
for g in ['dice', 'limbo', 'peg-drop', 'towers', 'keno']:
    print(g, sorted(os.listdir(f'{PUB}/{g}')))
