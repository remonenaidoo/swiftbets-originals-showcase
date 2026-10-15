import { Application, Assets, Container, Graphics, Sprite, Texture, type Ticker } from 'pixi.js';
import type { GameInfo, LineWin, Sym } from './api';
import { ReelSet } from './reels';
import { Tweens, ease } from './tweens';

export const SYMBOLS: Sym[] = ['Lion', 'Elephant', 'Rhino', 'Buffalo', 'Ace', 'King', 'Queen', 'Jack', 'Ten', 'Wild', 'Scatter'];
const FILE: Record<Sym, string> = {
  Lion: 'lion', Elephant: 'elephant', Rhino: 'rhino', Buffalo: 'buffalo', Ace: 'ace', King: 'king', Queen: 'queen',
  Jack: 'jack', Ten: 'ten', Wild: 'wild', Scatter: 'scatter',
};
const LINE_COLOURS = [0xffd36b, 0x46e0c8, 0xff7a59, 0xc58bff, 0x7cd3ff, 0xffe9a8, 0x9dff8a, 0xff9ad5, 0xffb347, 0x6bf0ff];

export const assetUrl = (path: string) => `${import.meta.env.BASE_URL}assets/${path}`;

/** Everything drawn on the canvas: background, logo, the reel frame, the reels, win lines and particles. */
export class Scene {
  readonly app = new Application();
  tweens!: Tweens;
  reels!: ReelSet;
  textures!: Record<Sym, Texture>;
  private bg = new Sprite();
  private bgPortrait!: Texture;
  private bgLandscape!: Texture;
  private logo = new Sprite();
  private frame = new Graphics();
  private glow = new Graphics();
  private lines = new Graphics();
  private fx = new Container();
  private tint = new Graphics();
  private board = new Container();
  private coin!: Texture;
  cellW = 100;
  cellH = 100;
  private gap = 6;

  async init(host: HTMLElement, onProgress: (p: number) => void) {
    await this.app.init({ resizeTo: host, background: 0x0b1d22, antialias: true, resolution: Math.min(2, window.devicePixelRatio || 1), autoDensity: true });
    host.appendChild(this.app.canvas);
    const files = [...SYMBOLS.map((s) => `symbols/${FILE[s]}.webp`), 'bg-portrait.jpg', 'bg-landscape.jpg', 'logo.webp'];
    const loaded = await Assets.load(files.map(assetUrl), onProgress);
    this.textures = Object.fromEntries(SYMBOLS.map((s) => [s, loaded[assetUrl(`symbols/${FILE[s]}.webp`)]])) as Record<Sym, Texture>;
    this.bgPortrait = loaded[assetUrl('bg-portrait.jpg')];
    this.bgLandscape = loaded[assetUrl('bg-landscape.jpg')];
    this.logo.texture = loaded[assetUrl('logo.webp')];
    this.logo.anchor.set(0.5, 0);
    this.coin = this.app.renderer.generateTexture(new Graphics().circle(0, 0, 10).fill(0xf6c453).circle(0, 0, 6).fill(0xffe9a8));
    this.tweens = new Tweens(this.app.ticker);
    this.reels = new ReelSet(this.textures, this.tweens, this.app.ticker);
    this.board.addChild(this.glow, this.frame, this.reels, this.lines);
    this.app.stage.addChild(this.bg, this.tint, this.logo, this.board, this.fx);
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
    this.bg.texture = portrait ? this.bgPortrait : this.bgLandscape;
    const cover = Math.max(w / this.bg.texture.width, h / this.bg.texture.height);
    this.bg.scale.set(cover);
    this.bg.position.set((w - this.bg.texture.width * cover) / 2, (h - this.bg.texture.height * cover) / 2);
    this.tint.clear().rect(0, 0, w, h).fill(0x6a1b5a);

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
      .roundRect(-pad, -pad, boardW + pad * 2, boardH + pad * 2, pad * 1.4).stroke({ width: Math.max(3, pad * 0.45), color: 0xd9a441 })
      .roundRect(-pad * 0.55, -pad * 0.55, boardW + pad * 1.1, boardH + pad * 1.1, pad).stroke({ width: 1.5, color: 0x46e0c8, alpha: 0.55 });
    for (let i = 1; i < 5; i++) {
      const x = i * (cell + this.gap) - this.gap / 2;
      this.frame.moveTo(x, 4).lineTo(x, boardH - 4).stroke({ width: 1, color: 0xd9a441, alpha: 0.25 });
    }
    this.glow.clear().roundRect(-pad * 2, -pad * 2, boardW + pad * 4, boardH + pad * 4, pad * 2).fill({ color: 0xffb347, alpha: 0.10 });

    const logoScale = Math.min(logoH / this.logo.texture.height, (w * 0.8) / this.logo.texture.width);
    this.logo.scale.set(logoScale);
    this.logo.position.set(w / 2, portrait ? 18 : 10);
    return { portrait, boardBottom: top + boardH + pad, boardTop: top - pad };
  }

  setFeatureMood(on: boolean) {
    void this.tweens.to(this.tint.alpha, on ? 0.28 : 0, 600, (v) => (this.tint.alpha = v));
  }

  clearWins() {
    this.lines.clear();
    for (const r of this.reels.reels) for (let row = 0; row < 3; row++) {
      const c = r.cell(row);
      c.alpha = 1;
    }
  }

  private centre(reel: number, row: number) {
    return { x: reel * (this.cellW + this.gap) + this.cellW / 2, y: row * this.cellH + this.cellH / 2 };
  }

  /** Draws the winning lines, dims everything else and pulses the winning symbols. */
  async showWins(info: GameInfo, wins: LineWin[], scatterCells: [number, number][], turbo: boolean) {
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
      for (let reel = 0; reel < w.count; reel++) winning.add(`${reel}:${rows[reel]}`);
    });
    if (!winning.size) return;
    this.reels.reels.forEach((r, reel) => {
      for (let row = 0; row < 3; row++) r.cell(row).alpha = winning.has(`${reel}:${row}`) ? 1 : 0.38;
    });
    const sprites = [...winning].map((k) => { const [reel, row] = k.split(':').map(Number); return this.reels.reels[reel].cell(row); });
    const base = sprites.map((s) => s.scale.x);
    await this.tweens.to(0, 1, turbo ? 260 : 520, (v) => {
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
