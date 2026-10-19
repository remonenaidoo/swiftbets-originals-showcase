import { Application, Assets, Container, FillGradient, Graphics, Sprite, Text, Texture, type Ticker } from 'pixi.js';
import type { GameInfo, LineWin, LockedCell, Sym } from './api';
import { assetUrl } from './art';
import type { SlotConfig } from './config';
import { ReelSet } from './reels';
import { Tweens, ease } from './tweens';


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
  private labelLayer = new Container();
  private locked: LockedCell[] = [];
  private fx = new Container();
  private tint = new Graphics();
  private board = new Container();
  private coin!: Texture;
  private spark!: Texture;
  private teaseGlow = new Graphics();
  private amount = new Text({ text: '', style: { fontFamily: 'system-ui, sans-serif', fontWeight: '900', fontSize: 64, fill: 0xffe08a, stroke: { color: 0x3a1a00, width: 8 }, dropShadow: { color: 0x000000, alpha: 0.7, distance: 4, blur: 10 } } });
  cellW = 100;
  cellH = 100;
  private gap = 6;
  private logoScale = 1;
  private teaseId = 0;
  private boardH = 0;

  constructor(private readonly config: SlotConfig) {}

  async init(host: HTMLElement, info: GameInfo, onProgress: (p: number) => void) {
    await this.app.init({ resizeTo: host, background: this.config.colours.background, antialias: true, resolution: Math.min(3, window.devicePixelRatio || 1), autoDensity: true });
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
    const spark = new Graphics();
    for (let r = 8; r > 0; r--) spark.circle(0, 0, r).fill({ color: 0xffffff, alpha: 0.08 + (8 - r) * 0.11 });
    this.spark = this.app.renderer.generateTexture(spark);
    this.tweens = new Tweens(this.app.ticker);
    const emphasised = new Set(info.symbols.filter((s) => s.kind !== 'Regular').map((s) => s.name));
    this.reels = new ReelSet(this.textures, emphasised, this.tweens, this.app.ticker);
    this.board.addChild(this.glow, this.frame, this.teaseGlow, this.reels, this.locks, this.labelLayer, this.lines, this.amount);
    this.amount.anchor.set(0.5);
    this.amount.visible = false;
    // The logo breathes gently while the game is open.
    let t = 0;
    this.app.ticker.add((tk) => { t += tk.deltaMS; const k = 1 + Math.sin(t / 900) * 0.018; this.logo.scale.set(this.logoScale * k); });
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

    // The HUD is HTML over the canvas: measure it, so a compact phone HUD gives its height back to the reels.
    const hud = document.getElementById('hud')?.getBoundingClientRect().height || (portrait ? 150 : 110);
    const logoH = portrait ? Math.min(h * 0.12, 120) : Math.min(h * 0.15, 130);
    const availW = portrait ? w - 12 : w * 0.7;
    const availH = h - hud - logoH - (portrait ? 22 : 28);
    // 5 cells + gaps + the frame's padding on both sides ≈ 5.45 cells wide, 3.25 tall.
    const cell = Math.floor(Math.min(availW / 5.45, availH / 3.25));
    // Phones: the reels take the full width; rows grow taller when there is room, never shorter than square.
    this.cellW = portrait ? Math.floor(availW / 5.45) : cell;
    this.cellH = portrait ? Math.floor(Math.max(cell, Math.min(this.cellW * 1.3, availH / 3.25))) : cell;
    this.gap = Math.max(2, Math.round(this.cellW * 0.04));
    const boardW = this.cellW * 5 + this.gap * 4, boardH = this.cellH * 3;
    const top = logoH + (portrait ? 14 : 16) + Math.max(0, (availH - boardH) / 2);
    this.board.position.set(Math.round((w - boardW) / 2), Math.round(top));
    this.reels.layout(this.cellW, this.cellH, this.gap);

    const pad = Math.round(Math.min(this.cellW, this.cellH) * 0.12);
    this.frame.clear()
      .roundRect(-pad, -pad, boardW + pad * 2, boardH + pad * 2, pad * 1.4).fill({ color: 0x06161a, alpha: 0.78 })
      .roundRect(-pad, -pad, boardW + pad * 2, boardH + pad * 2, pad * 1.4).stroke({ width: Math.max(3, pad * 0.45), color: this.config.colours.frame })
      .roundRect(-pad * 0.55, -pad * 0.55, boardW + pad * 1.1, boardH + pad * 1.1, pad).stroke({ width: 1.5, color: this.config.colours.accent, alpha: 0.55 });
    for (let i = 1; i < 5; i++) {
      const x = i * (this.cellW + this.gap) - this.gap / 2;
      this.frame.moveTo(x, 4).lineTo(x, boardH - 4).stroke({ width: 1, color: this.config.colours.frame, alpha: 0.25 });
    }
    this.glow.clear().roundRect(-pad * 2, -pad * 2, boardW + pad * 4, boardH + pad * 4, pad * 2).fill({ color: 0xffb347, alpha: 0.10 });

    this.logo.scale.set(1);
    // Phones: narrow enough to clear the clock pill and the corner.
    const logoScale = Math.min(logoH / this.logo.height, (w * (portrait ? 0.5 : 0.8)) / this.logo.width);
    this.logoScale = logoScale;
    this.logo.scale.set(logoScale);
    this.amount.position.set(boardW / 2, boardH / 2);
    this.amount.style.fontSize = Math.round(Math.min(this.cellW, this.cellH) * 0.62);
    this.boardH = boardH;
    this.drawLocks();
    this.logo.position.set(w / 2, portrait ? 8 : 10);
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
      this.flash(l.reel, l.row);
      this.sparks(l.reel, l.row, 18, [0xffe08a, 0xffc94a, 0xffffff]);
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
    this.amount.visible = false;
    this.teaseGlow.clear();
    for (const tile of this.locks.children) tile.alpha = 1;
    for (const r of this.reels.reels) for (let row = 0; row < 3; row++) {
      const c = r.cell(row);
      c.alpha = 1;
    }
  }

  private centre(reel: number, row: number) {
    return { x: reel * (this.cellW + this.gap) + this.cellW / 2, y: row * this.cellH + this.cellH / 2 };
  }

  /** Frames the winning symbols, dims everything else and pulses them. Paylines are not drawn; the rules show them. */
  async showWins(info: GameInfo, wins: LineWin[], scatterCells: [number, number][], pulseMs: number) {
    const cells: [number, number][] = [...scatterCells];
    for (const w of wins) {
      const rows = info.paylines[w.line];
      for (let reel = 0; reel < w.reels; reel++) cells.push([reel, rows[reel]]);
    }
    await this.showCells(cells, pulseMs);
  }

  /**
   * Values written on cells (a hold's coin values), above the symbols and locks; [] clears them. `colour` is the
   * text fill; a label pops in when it is new.
   */
  setLabels(labels: { reel: number; row: number; text: string; colour?: number }[]) {
    const before = new Set(this.labelLayer.children.map((c) => c.label));
    this.labelLayer.removeChildren().forEach((c) => c.destroy());
    for (const l of labels) {
      const { x, y } = this.centre(l.reel, l.row);
      const t = new Text({ text: l.text, style: { fontFamily: 'system-ui, sans-serif', fontWeight: '900', fontSize: Math.round(Math.min(this.cellW, this.cellH) * 0.24), fill: l.colour ?? 0xffffff, stroke: { color: 0x150a02, width: 5 }, dropShadow: { color: 0x000000, alpha: 0.7, distance: 2, blur: 4 }, padding: 8 } });
      t.anchor.set(0.5);
      t.position.set(x, y + this.cellH * 0.3);
      t.label = `${l.reel}:${l.row}:${l.text}`;
      this.labelLayer.addChild(t);
      if (!before.has(t.label)) void this.tweens.to(1.6, 1, 320, (v) => t.scale.set(v), ease.outBack);
    }
  }

  /** Frames any set of cells (ways, pay-anywhere, scatters), dims the rest and pulses them. */
  async showCells(cells: [number, number][], pulseMs: number) {
    this.clearWins();
    const winning = new Set<string>(cells.map(([r, row]) => `${r}:${row}`));
    if (!winning.size) return;
    const inset = this.cellW * 0.04;
    for (const k of winning) {
      const [reel, row] = k.split(':').map(Number);
      const c = this.centre(reel, row);
      this.lines.roundRect(c.x - this.cellW / 2 + inset, c.y - this.cellH / 2 + inset, this.cellW - inset * 2, this.cellH - inset * 2, this.cellW * 0.12)
        .stroke({ width: Math.max(2, this.cellW * 0.035), color: this.config.colours.accent, alpha: 0.95 });
    }
    this.reels.reels.forEach((r, reel) => {
      for (let row = 0; row < 3; row++) r.cell(row).alpha = winning.has(`${reel}:${row}`) ? 1 : 0.38;
    });
    for (const tile of this.locks.children) tile.alpha = winning.has(tile.label) ? 1 : 0.45;
    for (const k of winning) { const [reel, row] = k.split(':').map(Number); this.sparks(reel, row, 7, [0xffe08a, 0xffffff]); }
    // A locked cell shows its lock tile, so that is what pulses.
    const sprites = [...winning].map((k) => this.locks.children.find((c) => c.label === k) ?? this.reels.reels[+k.split(':')[0]].cell(+k.split(':')[1]));
    const base = sprites.map((s) => s.scale.x);
    await this.tweens.to(0, 1, pulseMs, (v) => {
      const k = 1 + Math.sin(v * Math.PI) * 0.14;
      sprites.forEach((s, i) => s.scale.set(base[i] * k));
    }, ease.linear);
    sprites.forEach((s, i) => s.scale.set(base[i]));
  }

  /** The win amount over the middle of the reels, popping in; hidden again by clearWins. */
  showAmount(text: string) {
    this.amount.text = text;
    this.amount.visible = true;
    void this.tweens.to(0.4, 1, 420, (v) => this.amount.scale.set(v), ease.outBack);
  }

  /** Lights a reel that is still spinning while it could land the third Scatter; null clears it. */
  tease(reel: number | null) {
    this.teaseId++;
    this.teaseGlow.clear();
    this.teaseGlow.alpha = 1;
    if (reel === null || reel > 4) return;
    const x = reel * (this.cellW + this.gap), pad = this.cellW * 0.06;
    this.teaseGlow.roundRect(x - pad, -pad, this.cellW + pad * 2, this.boardH + pad * 2, pad * 2).fill({ color: 0xffb020, alpha: 0.22 })
      .roundRect(x - pad, -pad, this.cellW + pad * 2, this.boardH + pad * 2, pad * 2).stroke({ width: Math.max(3, pad), color: 0xffd36b, alpha: 0.95 });
    const id = ++this.teaseId;
    void this.tweens.to(0, 1, 2400, (v) => { if (id === this.teaseId) this.teaseGlow.alpha = 0.6 + Math.sin(v * Math.PI * 8) * 0.4; }, ease.linear);
  }

  /** A Scatter has landed: it bounces and throws sparks. */
  scatterLanded(reel: number, row: number) {
    this.sparks(reel, row, 26, [0xffb020, 0xff6a1a, 0xffe08a, 0xffffff]);
    const cell = this.reels.reels[reel].cell(row), base = cell.scale.x;
    void this.tweens.to(0, 1, 360, (v) => cell.scale.set(base * (1 + Math.sin(v * Math.PI) * 0.22)), ease.linear).then(() => cell.scale.set(base));
  }

  /** An expanding gold ring from a cell. */
  private flash(reel: number, row: number) {
    const { x, y } = this.centre(reel, row);
    const ring = new Graphics().circle(0, 0, Math.min(this.cellW, this.cellH) * 0.5).stroke({ width: 6, color: 0xffe08a });
    ring.position.set(x, y);
    this.board.addChild(ring);
    void this.tweens.to(0, 1, 520, (v) => { ring.scale.set(0.6 + v * 0.9); ring.alpha = 1 - v; }, ease.outCubic).then(() => ring.destroy());
  }

  /** Sparks flying out of a cell, falling and fading. */
  sparks(reel: number, row: number, count: number, colours: number[]) {
    const c = this.centre(reel, row);
    for (let i = 0; i < count; i++) {
      const s = new Sprite(this.spark);
      s.anchor.set(0.5);
      s.blendMode = 'add';
      s.tint = colours[i % colours.length];
      s.position.set(c.x, c.y);
      const size = (0.35 + Math.random() * 0.6) * (this.cellW / 90);
      s.scale.set(size);
      this.board.addChild(s);
      const a = Math.random() * Math.PI * 2, v = (2 + Math.random() * 5) * (this.cellW / 90);
      let vx = Math.cos(a) * v, vy = Math.sin(a) * v - 2, life = 0;
      const ttl = 30 + Math.random() * 25;
      const step = (tk: Ticker) => {
        const d = tk.deltaTime;
        life += d; vy += 0.18 * d; vx *= 0.98;
        s.x += vx * d; s.y += vy * d;
        s.alpha = Math.max(0, 1 - life / ttl);
        if (life >= ttl) { this.app.ticker.remove(step); s.destroy(); }
      };
      this.app.ticker.add(step);
    }
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
