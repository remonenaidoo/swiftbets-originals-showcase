import { BlurFilter, Container, Graphics, Sprite, type Texture, type Ticker } from 'pixi.js';
import type { Sym } from './api';
import type { Speed } from './speed';
import { ease, type Tweens } from './tweens';

/**
 * One reel. `pos` is the strip index shown in the top row; it decreases while spinning, so symbols travel down.
 * At rest `pos` is an integer, and rows 0..2 show strip[pos], strip[pos+1], strip[pos+2]: exactly the window the
 * server evaluated for that stop.
 */
export class Reel extends Container {
  private strip: Sym[] = [];
  private readonly cells: Sprite[] = [];
  private readonly mask_ = new Graphics();
  private readonly blur = new BlurFilter({ strength: 0, quality: 2 });
  private pos = 0;
  private speed = 0; // cells per second while spinning
  private pendingSpeed = 0;
  private startIn = 0; // ms until a staggered spin starts
  private target: number | null = null;
  private landing: ((value: void) => void) | null = null;
  private landMs = 210;
  private cellW = 100;
  private cellH = 100;

  constructor(private readonly textures: Record<Sym, Texture>, private readonly emphasised: Set<Sym>, private readonly tweens: Tweens, ticker: Ticker) {
    super();
    const inner = new Container();
    for (let k = 0; k < 4; k++) {
      const s = new Sprite();
      s.anchor.set(0.5);
      this.cells.push(s);
      inner.addChild(s);
    }
    inner.filters = [this.blur];
    this.addChild(inner, this.mask_);
    inner.mask = this.mask_;
    ticker.add((t) => this.update(t.deltaMS));
  }

  setStrip(strip: Sym[], stop: number) {
    this.strip = strip;
    this.pos = stop;
    this.render();
  }

  layout(cellW: number, cellH: number) {
    this.cellW = cellW;
    this.cellH = cellH;
    this.mask_.clear().rect(0, 0, cellW, cellH * 3).fill(0xffffff);
    this.render();
  }

  /** Starts spinning after `delay` ms (reels start one after another). A stop before then starts it at once. */
  spin(speed: number, delay = 0) {
    this.target = null;
    this.pendingSpeed = speed;
    this.startIn = delay;
    if (delay <= 0) this.speed = speed;
  }

  /**
   * Lands on `stop`, bouncing for `landMs`; resolves when the reel is at rest. While the reel is blurred the strip
   * is re-based so the stop is a few cells away (the way slot reels swap in their result), instead of rolling the
   * rest of the strip round to reach it.
   */
  stop(stop: number, landMs: number): Promise<void> {
    this.landMs = landMs;
    if (this.speed <= 0) {
      this.speed = this.pendingSpeed || 28;
      this.startIn = 0;
    }
    const lead = 3.4 + (this.pos - Math.floor(this.pos));
    this.target = stop;
    this.pos = stop + lead;
    return new Promise((r) => (this.landing = r));
  }

  /** The sprite in a visible row, for win highlights (valid at rest). */
  cell(row: number): Sprite {
    return this.cells[row];
  }

  get spinning() {
    return this.speed > 0 || this.target !== null;
  }

  private update(ms: number) {
    if (this.startIn > 0) {
      this.startIn -= ms;
      if (this.startIn > 0) return;
      if (this.speed <= 0) this.speed = this.pendingSpeed;
    }
    if (this.speed <= 0) return;
    if (this.target !== null && this.pos - this.speed * (ms / 1000) <= this.target + 3) {
      const from = this.pos, to = this.target, done = this.landing;
      this.speed = 0;
      this.target = null;
      this.landing = null;
      this.blur.strengthY = 0;
      void this.tweens.to(from, to, this.landMs, (v) => { this.pos = v; this.render(); }, ease.outBack).then(() => {
        this.pos = Math.round(to);
        this.render();
        done?.();
      });
      return;
    }
    this.pos -= this.speed * (ms / 1000);
    this.blur.strengthY = Math.min(14, this.speed * 0.45);
    this.render();
  }

  private render() {
    const n = this.strip.length;
    if (!n) return;
    const i0 = Math.floor(this.pos);
    const frac = this.pos - i0;
    const size = Math.min(this.cellW, this.cellH) * 0.92;
    this.cells.forEach((s, k) => {
      const sym = this.strip[(((i0 + k) % n) + n) % n];
      s.texture = this.textures[sym];
      const scale = size / Math.max(s.texture.width, s.texture.height);
      s.scale.set(scale * (this.emphasised.has(sym) ? 1.06 : 1));
      s.position.set(this.cellW / 2, (k - frac) * this.cellH + this.cellH / 2);
      s.alpha = 1;
      s.tint = 0xffffff;
    });
  }
}

/** Five reels, spun together and stopped one after another, with anticipation on the last reels. */
export class ReelSet extends Container {
  readonly reels: Reel[] = [];

  constructor(textures: Record<Sym, Texture>, emphasised: Set<Sym>, tweens: Tweens, ticker: Ticker) {
    super();
    for (let i = 0; i < 5; i++) {
      const r = new Reel(textures, emphasised, tweens, ticker);
      this.reels.push(r);
      this.addChild(r);
    }
  }

  setStrips(strips: Sym[][], stops: number[]) {
    this.reels.forEach((r, i) => r.setStrip(strips[i], stops[i]));
  }

  layout(cellW: number, cellH: number, gap: number) {
    this.reels.forEach((r, i) => {
      r.position.set(i * (cellW + gap), 0);
      r.layout(cellW, cellH);
    });
  }

  private hurry: (() => void) | null = null;
  private slammed = false;

  /** Which symbol is the Scatter, for anticipation; set by the game. */
  scatter: (s: Sym) => boolean = () => false;

  get spinning() {
    return this.reels.some((r) => r.spinning);
  }

  spin(speed: Speed) {
    this.slammed = false;
    this.reels.forEach((r, i) => r.spin(speed.reelSpeed, i * speed.startGap));
  }

  /** The player tapped to stop: the reels land together as soon as the result is in, skipping the gaps. */
  slam() {
    this.slammed = true;
    this.hurry?.();
  }

  private gap(ms: number): Promise<void> {
    if (ms <= 0) return Promise.resolve();
    return new Promise((done) => {
      const t = setTimeout(finish, ms);
      function finish() { clearTimeout(t); done(); }
      this.hurry = () => { this.hurry = null; finish(); };
    });
  }

  /**
   * Stops left to right, each reel starting its landing `stopGap` after the one before (they overlap). A reel that
   * could complete three Scatters waits `tease` longer (anticipation). `onStop` fires as each reel lands.
   */
  async stop(stops: number[], strips: Sym[][], speed: Speed, onStop: (reel: number, tease: boolean) => void) {
    const count = (reel: number) => {
      let n = 0;
      for (let row = 0; row < 3; row++) if (this.scatter(strips[reel][(stops[reel] + row) % strips[reel].length])) n++;
      return n;
    };
    const landed: Promise<void>[] = [];
    let scatters = 0;
    for (let i = 0; i < 5; i++) {
      const tease = i >= 3 && scatters >= 2;
      if (!this.slammed && i > 0) await this.gap(speed.stopGap + (tease ? speed.tease : 0));
      const shownScatters = scatters;
      landed.push(this.reels[i].stop(stops[i], this.slammed ? 120 : speed.land).then(() => onStop(i, i >= 2 && i < 4 && shownScatters + count(i) >= 2)));
      scatters += count(i);
    }
    this.hurry = null;
    await Promise.all(landed);
  }
}
