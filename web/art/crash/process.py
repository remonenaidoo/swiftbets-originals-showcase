"""Turns the chosen Crash renders into game layers. Run with the rembg venv: ~/.venvs/rembg/bin/python process.py
rocket: cut out, cropped, its nose angle measured (printed; the game rotates by it).
city: the Cape Town strip, its sky faded to transparent so the game's sky shows through.
clouds: black made transparent (alpha from brightness). space: as is, landscape and portrait crops."""
import math, os
import numpy as np
from PIL import Image
from rembg import new_session, remove

RAW, OUT = 'raw', '../../public/assets/crash'
os.makedirs(OUT, exist_ok=True)

rocket = remove(Image.open(f'{RAW}/rocket2.png').convert('RGB'), session=new_session('isnet-general-use'))
a = np.array(rocket.getchannel('A'))
rocket = rocket.crop(Image.fromarray((a > 60).astype(np.uint8) * 255).getbbox())
# The render's own exhaust streak trails off to the lower left: cut that corner (the game draws its own exhaust).
r = np.array(rocket)
h, w = r.shape[:2]
yy, xx = np.mgrid[0:h, 0:w]
r[..., 3][(xx / w + (1 - yy / h)) < 0.62] = 0
rocket = Image.fromarray(r)
rocket = rocket.crop(Image.fromarray((np.array(rocket.getchannel('A')) > 60).astype(np.uint8) * 255).getbbox())
# The hull's long axis, from the solid pixels: the principal component, pointing away from the engine glow.
m = np.array(rocket.getchannel('A')) > 200
ys, xs = np.nonzero(m)
pts = np.stack([xs - xs.mean(), ys - ys.mean()])
w, v = np.linalg.eigh(np.cov(pts))
axis = v[:, np.argmax(w)]
if axis[0] < 0: axis = -axis  # the nose is to the right (up-right in this render)
print('rocket nose angle (rad):', round(math.atan2(axis[1], axis[0]), 4), 'size', rocket.size)
# The nozzle: the hull's rear-most solid point along the axis (the game emits the exhaust there).
m2 = np.array(rocket.getchannel('A')) > 200
ys2, xs2 = np.nonzero(m2)
proj = (xs2 - xs2.mean()) * axis[0] + (ys2 - ys2.mean()) * axis[1]
k = np.argmin(proj)
print('nozzle fraction:', round(xs2[k] / rocket.width, 3), round(ys2[k] / rocket.height, 3))
rocket.thumbnail((640, 640), Image.LANCZOS)
rocket.save(f'{OUT}/rocket.webp', quality=92, method=6)

city = Image.open(f'{RAW}/city.png').convert('RGBA')
city = city.crop((0, int(city.height * 0.38), city.width, city.height))
alpha = np.ones((city.height, city.width), np.float32)
fade = int(city.height * 0.45)
alpha[:fade] = np.linspace(0, 1, fade)[:, None] ** 1.6
c = np.array(city).astype(np.float32)
c[..., 3] = alpha * 255
city = Image.fromarray(c.astype(np.uint8))
city.thumbnail((2400, 2400), Image.LANCZOS)
city.save(f'{OUT}/city.webp', quality=88, method=6)

clouds = np.array(Image.open(f'{RAW}/clouds.png').convert('RGB')).astype(np.float32)
lum = clouds.max(axis=2)
alpha = np.clip((lum - 8) * 2.2, 0, 255)
cl = Image.fromarray(np.dstack([clouds, alpha]).astype(np.uint8), 'RGBA')
cl.thumbnail((2400, 2400), Image.LANCZOS)
cl.save(f'{OUT}/clouds.webp', quality=86, method=6)

space = Image.open(f'{RAW}/space.png').convert('RGB')
space.resize((1920, 1072), Image.LANCZOS).save(f'{OUT}/space-landscape.jpg', quality=86)
sw = int(space.height * 9 / 16)
space.crop(((space.width - sw) // 2, 0, (space.width + sw) // 2, space.height)).resize((1080, 1920), Image.LANCZOS).save(f'{OUT}/space-portrait.jpg', quality=86)
for f in sorted(os.listdir(OUT)): print(f, os.path.getsize(f'{OUT}/{f}') // 1024, 'KB')
