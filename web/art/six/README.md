# The six instant games' artwork

Sunburst Wheel, Coin Flip, Lucky Scratch, Gem Hand, Baccarat, Higher or Lower.

Generated with Flux 1.1 Pro / Ultra (BFL) from `prompts.json` by `../gen.py`, the same pipeline as `../five`.
`concept-sheet.jpg` (one Ultra 16:9 render, six panels) set the look: glossy stylised 3D, gold accents,
deep velvet / felt / teal grounds, one colour identity per game. Raw renders live in `raw/` (not committed).

    export BFL_API_KEY=...            # from ~/.config/claude/secrets.env
    python3 batch.py <item>[:seed] …  # 3 renders at a time, retries on 429 → raw/<item>[-s<seed>].png
    ~/.venvs/rembg/bin/python process.py          # cut-outs + backgrounds → ../../public/assets/<game>/
    cd ../instant && python3 covers.py sunburst-wheel coin-flip lucky-scratch gem-hand baccarat higher-lower

## Chosen renders
- hub = `sun-hub-s52`, heads = `coin-heads-s23`, tails = `coin-tails-s23` (a matched pair: same rim and tone),
  gem-2 = `gem-2-s3`, Higher or Lower card back = `hl-back-s5`; everything else is the unseeded first render.

## How the cut-outs were made (process.py)
- rembg (isnet-general-use) **zeroes the colour** wherever it cuts, and it half-cuts flat or glassy fields.
  So nothing takes rembg's RGB: the original pixels are kept and rembg only supplies the outline.
- Coins and hub: masked to a true anti-aliased circle (`round_sprite`).
- Card backs: rendered on white; the card is the non-white bbox, masked to a rounded rectangle; the
  Higher or Lower back has its gold flecks desaturated to silver.
- Clover, diamond, gems: `solid` fills see-through interiors and hardens the alpha; the diamond's flare rays
  are removed by a morphological opening. Horseshoe / bell / crown: alpha hardened only (keeps the openwork).
- Star: the render had a ring behind it, so its alpha is a fitted five-point polygon (`star_sprite`).
- Backgrounds are centre-cropped to exact 16:9 / 9:16 (no stretching). Higher or Lower's landscape had
  magenta neon and its portrait a gold ring: pulled to navy + silver so the pair match. Baccarat's dim
  dealer-tray shape at the top is blurred so it cannot read as glyphs.

## Not made
- `lucky-scratch/card.webp` (scratch-card frame): the dark enamel border could not be separated cleanly from
  its black ground (smudges, or a see-through border with a luminance key). Skipped; draw the frame in code.
  The render is `raw/scratch-card.png` and `scratch_frame()` is still in process.py.
- BFL credits ran out (HTTP 402) mid-way, so the frame and the star/diamond could not be re-rendered;
  the star and diamond were fixed in processing instead.
