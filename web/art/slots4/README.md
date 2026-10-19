# Karoo Stargazer, Sardine Run, Kimberley Hold, Fynbos Bloom artwork

Generated with Flux 1.1 Pro / Ultra (BFL) from `prompts.json` (same house style string as `../slots3`), using `../gen.py`
through `run.py` (3 jobs at a time, retries on 429; `name@seed` overrides a seed and writes `raw/<name>-s<seed>.png`).
Raw renders stay out of git (`raw/`).

1. `concept-sheet.jpg` set the look first: one ultra 16:9 render of all four worlds side by side (`sheet`, seed 14).
   Earlier sheet tries (no seed, seeds 7, 21) came back covered in made-up captions; asking for "a wordless panorama in
   four slices" fixed that.
2. Every symbol, logo and background was then rendered on its own (no seed unless listed below).
3. `process.py` (rembg venv: `~/.venvs/rembg/bin/python process.py`) cuts symbols out with rembg isnet-general-use,
   crops them to their solid pixels and scales them UP to fill a 512 px cell. It also builds Sardine Run's ×2/×3 Wilds
   (Cape Point Ways' badge), Kimberley Hold's blank hold tile, the logos (rembg cut-outs, at most 1000 px wide) and the
   1920×1080 / 1080×1920 backgrounds. Per-render repairs, each found by inspecting the symbol at full size:
   - `kim-coin`: the render's centre was black, so code fills it with polished gold, left plain for the printed value.
   - `fyn-sun`: the render showed a pointed peak, so code repaints the inside of the ring as a sun rising behind a
     flat-topped Table Mountain, using the render's own rock texture.
   - glowing renders (`run-scatter`, `fyn-bloom`, `fyn-sun`) take extra alpha from brightness, so the glow isn't cut away.
   - small fixes: the kudu's leftover ring arc, the Q's black counter, the leopard's smoky base, the dolphin's spray
     smudge, the restio's dark fringe, and letterbox bars on the Kimberley landscape.
4. `covers.py` builds each 600×600 `cover.jpg`: the portrait backdrop, the hero symbol with a glow, the logo, and
   SWIFTBETS ORIGINALS underneath (same layout as `../instant/covers.py` slot covers).

Chosen variants (seeded re-renders): `star-kudu` s11, `star-meerkat` s11, `star-ostrich` s11, `run-hook` s29,
`run-wild` s29, `kim-mule` s11. All other assets use the first (unseeded) render. All four logos were checked letter by letter.

Known weak spots. The BFL account ran out of credits mid-run, so these couldn't be re-rendered:
- `fynbos-bloom/symbols/disa.webp` is a generic red lily-like flower, not a botanically exact Disa uniflora.
- `fynbos-bloom/symbols/restio.webp` reads more like wheat ears than restio reeds.
- `fynbos-bloom/symbols/dewdrop.webp` is a glassy drop with a leaf sprig inside it, not a drop resting on a leaf.
- The Kimberley diamonds look more cut than rough.
