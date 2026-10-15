import { BlurFilter, Container, Graphics, Sprite, type Texture, type Ticker } from 'pixi.js';
import type { Sym } from './api';
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
  private cellW = 100;
  private cellH = 100;

  constructor(private readonly textures: Record<Sym, Texture>, private readonly tweens: Tweens, ticker: Ticker) {
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

  /** Lands on `stop` after spinning at least a few more cells; resolves when the reel is at rest. */
  stop(stop: number): Promise<void> {
    if (this.speed <= 0) {
      this.speed = this.pendingSpeed || 26;
      this.startIn = 0;
    }
    const n = this.strip.length;
    const min = this.pos - 3.5;
    let t = Math.floor(min) - (((Math.floor(min) - stop) % n) + n) % n;
    if (t > min) t -= n;
    this.target = t;
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
      void this.tweens.to(from, to, (3 / 26) * 1000 * 1.6, (v) => { this.pos = v; this.render(); }, ease.outBack).then(() => {
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
      s.scale.set(scale * (sym === 'Wild' || sym === 'Scatter' ? 1.06 : 1));
      s.position.set(this.cellW / 2, (k - frac) * this.cellH + this.cellH / 2);
      s.alpha = 1;
      s.tint = 0xffffff;
    });
  }
}

/** Five reels, spun together and stopped one after another, with anticipation on the last reels. */
export class ReelSet extends Container {
  readonly reels: Reel[] = [];

  constructor(textures: Record<Sym, Texture>, private readonly tweens: Tweens, ticker: Ticker) {
    super();
    for (let i = 0; i < 5; i++) {
      const r = new Reel(textures, tweens, ticker);
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

  spin(turbo: boolean) {
    this.reels.forEach((r, i) => r.spin(turbo ? 34 : 26, turbo ? 0 : i * 60));
  }

  /**
   * Stops left to right. When two Scatters are already showing after reel 3, the remaining reels slow down
   * (anticipation) before landing. `onStop` fires as each reel lands.
   */
  async stop(stops: number[], strips: Sym[][], turbo: boolean, onStop: (reel: number, tease: boolean) => void) {
    let scatters = 0;
    for (let i = 0; i < 5; i++) {
      const tease = i >= 3 && scatters >= 2;
      if (tease) await this.tweens.wait(turbo ? 300 : 900);
      else if (i > 0) await this.tweens.wait(turbo ? 40 : 130);
      await this.reels[i].stop(stops[i]);
      for (let row = 0; row < 3; row++) if (strips[i][(stops[i] + row) % strips[i].length] === 'Scatter') scatters++;
      onStop(i, i >= 2 && scatters >= 2 && i < 4);
    }
  }
}
