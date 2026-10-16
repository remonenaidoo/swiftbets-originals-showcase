import { Application, Assets, Container, FillGradient, Graphics, Sprite, Text, Texture, type Ticker } from 'pixi.js';
import type { GameInfo, LineWin, LockedCell, Sym } from './api';
import { assetUrl } from './art';
import type { SlotConfig } from './config';
import { ReelSet } from './reels';
import { Tweens, ease } from './tweens';

const LINE_COLOURS = [0xffd36b, 0x46e0c8, 0xff7a59, 0xc58bff, 0x7cd3ff, 0xffe9a8, 0x9dff8a, 0xff9ad5, 0xffb347, 0x6bf0ff];

/** Everything drawn on the canvas: background, logo, the reel frame, the reels, locked cells, win lines and particles. */
export class Scene {
  readonly app = new Application();
  tweens!: Tweens;
  reels!: ReelSet;
  textures!: Record<Sym, Texture>;
  private bg = new Sprite();
  private bgDrawn = new Graphics();
  private bgPortrait?: Texture;
  private bgLandscape?: Texture;
  private logo: Sprite | Text = new Sprite();
  private frame = new Graphics();
  private glow = new Graphics();
  private lines = new Graphics();
  private locks = new Container();
  private locked: LockedCell[] = [];
  private fx = new Container();
  private tint = new Graphics();
  private board = new Container();
  private coin!: Texture;
  cellW = 100;
  cellH = 100;
  private gap = 6;

  constructor(private readonly config: SlotConfig) {}

  async init(host: HTMLElement, info: GameInfo, onProgress: (p: number) => void) {
    await this.app.init({ resizeTo: host, background: this.config.colours.background, antialias: true, resolution: Math.min(2, window.devicePixelRatio || 1), autoDensity: true });
    host.appendChild(this.app.canvas);
    this.textures = await this.config.art(this.app, info, (p) => onProgress(p * 0.8));
    const extra = [this.config.background?.portrait, this.config.background?.landscape, this.config.logo].filter((x): x is string => !!x);
    const loaded = extra.length ? await Assets.load(extra.map(assetUrl), (p) => onProgress(0.8 + p * 0.2)) : {};
    if (this.config.background) {
      this.bgPortrait = loaded[assetUrl(this.config.background.portrait)];
      this.bgLandscape = loaded[assetUrl(this.config.background.landscape)];
    }
    if (this.config.logo) {
      (this.logo as Sprite).texture = loaded[assetUrl(this.config.logo)];
    } else {
      this.logo = new Text({ text: this.config.title.toUpperCase(), style: { fontFamily: 'system-ui, sans-serif', fontWeight: '900', fontSize: 72, letterSpacing: 6, fill: this.config.colours.frame, dropShadow: { color: 0x000000, alpha: 0.6, distance: 4, blur: 8 } } });
    }
    this.logo.anchor.set(0.5, 0);
    this.coin = this.app.renderer.generateTexture(new Graphics().circle(0, 0, 10).fill(0xf6c453).circle(0, 0, 6).fill(0xffe9a8));
    this.tweens = new Tweens(this.app.ticker);
    const emphasised = new Set(info.symbols.filter((s) => s.kind !== 'Regular').map((s) => s.name));
    this.reels = new ReelSet(this.textures, emphasised, this.tweens, this.app.ticker);
    this.board.addChild(this.glow, this.frame, this.reels, this.locks, this.lines);
    this.app.stage.addChild(this.bgDrawn, this.bg, this.tint, this.logo, this.board, this.fx);
    this.tint.alpha = 0;
    window.addEventListener('resize', () => this.layout());
  }

  get ticker(): Ticker {
    return this.app.ticker;
  }

  /** Sizes the reels to the screen: portrait leaves room for the logo above and the controls below. */
  layout() {
    const w = this.app.screen.width, h = this.app.screen.height;
    const portrait = h > w;
    const backdrop = portrait ? this.bgPortrait : this.bgLandscape;
    if (backdrop) {
      this.bg.texture = backdrop;
      const cover = Math.max(w / backdrop.width, h / backdrop.height);
      this.bg.scale.set(cover);
      this.bg.position.set((w - backdrop.width * cover) / 2, (h - backdrop.height * cover) / 2);
    } else {
      const fill = new FillGradient({ type: 'linear', start: { x: 0, y: 0 }, end: { x: 0, y: 1 }, colorStops: [{ offset: 0, color: this.config.colours.backgroundTop }, { offset: 1, color: this.config.colours.background }], textureSpace: 'local' });
      this.bgDrawn.clear().rect(0, 0, w, h).fill(fill);
    }
    this.tint.clear().rect(0, 0, w, h).fill(this.config.colours.featureTint);

    const hud = portrait ? 190 : 110;
    const logoH = portrait ? Math.min(h * 0.13, 120) : Math.min(h * 0.14, 110);
    const availW = w * (portrait ? 0.96 : 0.7);
    const availH = h - hud - logoH - (portrait ? 40 : 28);
    // 5 cells + gaps + the frame's padding on both sides ≈ 5.5 cells wide, 3.3 tall.
    const cell = Math.floor(Math.min(availW / 5.5, availH / 3.3));
    this.cellW = cell;
    // Phones are tall: taller rows use the height without shrinking the symbols (they fit the shorter side).
    this.cellH = portrait ? Math.floor(Math.min(cell * 1.3, availH / 3.3)) : cell;
    this.gap = Math.max(3, Math.round(cell * 0.05));
    const boardW = cell * 5 + this.gap * 4, boardH = this.cellH * 3;
    const top = logoH + (portrait ? 26 : 16) + Math.max(0, (availH - boardH) / 2);
    this.board.position.set(Math.round((w - boardW) / 2), Math.round(top));
    this.reels.layout(cell, this.cellH, this.gap);

    const pad = Math.round(cell * 0.12);
    this.frame.clear()
      .roundRect(-pad, -pad, boardW + pad * 2, boardH + pad * 2, pad * 1.4).fill({ color: 0x06161a, alpha: 0.78 })
      .roundRect(-pad, -pad, boardW + pad * 2, boardH + pad * 2, pad * 1.4).stroke({ width: Math.max(3, pad * 0.45), color: this.config.colours.frame })
      .roundRect(-pad * 0.55, -pad * 0.55, boardW + pad * 1.1, boardH + pad * 1.1, pad).stroke({ width: 1.5, color: this.config.colours.accent, alpha: 0.55 });
    for (let i = 1; i < 5; i++) {
      const x = i * (cell + this.gap) - this.gap / 2;
      this.frame.moveTo(x, 4).lineTo(x, boardH - 4).stroke({ width: 1, color: this.config.colours.frame, alpha: 0.25 });
    }
    this.glow.clear().roundRect(-pad * 2, -pad * 2, boardW + pad * 4, boardH + pad * 4, pad * 2).fill({ color: 0xffb347, alpha: 0.10 });

    this.logo.scale.set(1);
    const logoScale = Math.min(logoH / this.logo.height, (w * 0.8) / this.logo.width);
    this.logo.scale.set(logoScale);
    this.drawLocks();
    this.logo.position.set(w / 2, portrait ? 18 : 10);
    return { portrait, boardBottom: top + boardH + pad, boardTop: top - pad };
  }

  /** Shows the feature's locked cells over the reels (they stay put while the reels spin underneath); [] clears them. */
  setLocks(locked: LockedCell[]) {
    const added = locked.filter((l) => !this.locked.some((o) => o.reel === l.reel && o.row === l.row));
    this.locked = locked;
    this.drawLocks();
    for (const l of added) {
      const tile = this.locks.children.find((c) => c.label === `${l.reel}:${l.row}`);
      if (tile) void this.tweens.to(1.35, 1, 380, (v) => tile.scale.set(v), ease.outBack);
    }
  }

  private drawLocks() {
    this.locks.removeChildren().forEach((c) => c.destroy({ children: true }));
    const size = Math.min(this.cellW, this.cellH) * 0.92;
    for (const l of this.locked) {
      const { x, y } = this.centre(l.reel, l.row);
      const tile = new Container({ label: `${l.reel}:${l.row}` });
      tile.position.set(x, y);
      const back = new Graphics()
        .roundRect(-this.cellW / 2 + 2, -this.cellH / 2 + 2, this.cellW - 4, this.cellH - 4, 10).fill({ color: 0x0b0905, alpha: 0.92 })
        .roundRect(-this.cellW / 2 + 2, -this.cellH / 2 + 2, this.cellW - 4, this.cellH - 4, 10).stroke({ width: 3, color: this.config.colours.frame });
      const sprite = new Sprite(this.textures[l.symbol]);
      sprite.anchor.set(0.5);
      sprite.scale.set(size / Math.max(sprite.texture.width, sprite.texture.height));
      tile.addChild(back, sprite);
      this.locks.addChild(tile);
    }
  }

  setFeatureMood(on: boolean) {
    void this.tweens.to(this.tint.alpha, on ? 0.28 : 0, 600, (v) => (this.tint.alpha = v));
  }

  clearWins() {
    this.lines.clear();
    for (const tile of this.locks.children) tile.alpha = 1;
    for (const r of this.reels.reels) for (let row = 0; row < 3; row++) {
      const c = r.cell(row);
      c.alpha = 1;
    }
  }

  private centre(reel: number, row: number) {
    return { x: reel * (this.cellW + this.gap) + this.cellW / 2, y: row * this.cellH + this.cellH / 2 };
  }

  /** Draws the winning lines, dims everything else and pulses the winning symbols. */
  async showWins(info: GameInfo, wins: LineWin[], scatterCells: [number, number][], pulseMs: number) {
    this.clearWins();
    const winning = new Set<string>(scatterCells.map(([r, row]) => `${r}:${row}`));
    wins.forEach((w, i) => {
      const rows = info.paylines[w.line];
      const colour = LINE_COLOURS[i % LINE_COLOURS.length];
      const pts = rows.map((row, reel) => this.centre(reel, row));
      this.lines.moveTo(pts[0].x - this.cellW * 0.5, pts[0].y);
      pts.forEach((p) => this.lines.lineTo(p.x, p.y));
      this.lines.lineTo(pts[4].x + this.cellW * 0.5, pts[4].y);
      this.lines.stroke({ width: Math.max(3, this.cellW * 0.05), color: colour, alpha: 0.9, cap: 'round', join: 'round' });
      for (let reel = 0; reel < w.reels; reel++) winning.add(`${reel}:${rows[reel]}`);
    });
    if (!winning.size) return;
    this.reels.reels.forEach((r, reel) => {
      for (let row = 0; row < 3; row++) r.cell(row).alpha = winning.has(`${reel}:${row}`) ? 1 : 0.38;
    });
    for (const tile of this.locks.children) tile.alpha = winning.has(tile.label) ? 1 : 0.45;
    // A locked cell shows its lock tile, so that is what pulses.
    const sprites = [...winning].map((k) => this.locks.children.find((c) => c.label === k) ?? this.reels.reels[+k.split(':')[0]].cell(+k.split(':')[1]));
    const base = sprites.map((s) => s.scale.x);
    await this.tweens.to(0, 1, pulseMs, (v) => {
      const k = 1 + Math.sin(v * Math.PI) * 0.14;
      sprites.forEach((s, i) => s.scale.set(base[i] * k));
    }, ease.linear);
    sprites.forEach((s, i) => s.scale.set(base[i]));
  }

  /** A burst of gold coins from the reels: more coins for bigger wins. */
  coins(count: number) {
    const w = this.app.screen.width, h = this.app.screen.height;
    for (let i = 0; i < count; i++) {
      const s = new Sprite(this.coin);
      s.anchor.set(0.5);
      s.position.set(w / 2 + (Math.random() - 0.5) * this.cellW * 3, h * 0.45);
      s.scale.set(0.6 + Math.random() * 0.9);
      s.visible = false;
      this.fx.addChild(s);
      const vx = (Math.random() - 0.5) * 16, vy = -10 - Math.random() * 14, spin = (Math.random() - 0.5) * 0.4;
      let x = s.x, y = s.y, t = 0;
      const step = (tk: Ticker) => {
        s.visible = true;
        const d = tk.deltaTime;
        t += d;
        x += vx * d;
        y += (vy + 0.55 * t) * d;
        s.position.set(x, y);
        s.rotation += spin * d;
        s.scale.x = Math.abs(Math.cos(t * 0.15)) * 1.2;
        if (y > h + 40) {
          this.app.ticker.remove(step);
          s.destroy();
        }
      };
      setTimeout(() => this.app.ticker.add(step), Math.random() * 500);
    }
  }
}
