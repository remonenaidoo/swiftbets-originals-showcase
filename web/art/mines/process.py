"""Turns the Mines renders into game art. Run with the rembg venv: ~/.venvs/rembg/bin/python process.py"""
import os
import numpy as np
from PIL import Image
from rembg import new_session, remove

RAW, OUT = 'raw', '../../public/assets/mines'
os.makedirs(OUT, exist_ok=True)
session = new_session('isnet-general-use')

def cut(name, size=512):
    im = remove(Image.open(f'{RAW}/{name}.png').convert('RGB'), session=session)
    a = np.array(im.getchannel('A'))
    im = im.crop(Image.fromarray((a > 60).astype(np.uint8) * 255).getbbox())
    im.thumbnail((size, size), Image.LANCZOS)
    c = Image.new('RGBA', (size, size))
    c.alpha_composite(im, ((size - im.width) // 2, (size - im.height) // 2))
    c.save(f'{OUT}/{name}.webp', quality=92, method=6)

cut('diamond')
cut('mine')
# The tile: the square itself, found by brightness (its glowing edge), kept with its own dark face.
t = Image.open(f'{RAW}/tile.png').convert('RGB')
lum = np.array(t).max(axis=2)
box = Image.fromarray(((lum > 40) * 255).astype(np.uint8)).getbbox()
t = remove(t.crop(box), session=session)
t = t.resize((384, 384), Image.LANCZOS)
t.save(f'{OUT}/tile.webp', quality=92, method=6)
for side, size in [('landscape', (1920, 1080)), ('portrait', (1080, 1920))]:
    Image.open(f'{RAW}/bg-{side}.png').convert('RGB').resize(size, Image.LANCZOS).save(f'{OUT}/bg-{side}.jpg', quality=85)
for f in sorted(os.listdir(OUT)): print(f, os.path.getsize(f'{OUT}/{f}') // 1024, 'KB')
