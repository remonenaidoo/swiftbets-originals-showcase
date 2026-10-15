import type { Ticker } from 'pixi.js';

// A tiny tween runner on the Pixi ticker: enough for reels, pulses and count-ups.
export type Ease = (t: number) => number;
export const ease = {
  linear: (t: number) => t,
  outCubic: (t: number) => 1 - Math.pow(1 - t, 3),
  inOutSine: (t: number) => -(Math.cos(Math.PI * t) - 1) / 2,
  outBack: (t: number) => {
    const c1 = 1.25, c3 = c1 + 1;
    return 1 + c3 * Math.pow(t - 1, 3) + c1 * Math.pow(t - 1, 2);
  },
};

interface Job { from: number; to: number; ms: number; at: number; ease: Ease; set: (v: number) => void; done: () => void }

export class Tweens {
  private jobs: Job[] = [];
  private now = 0;

  constructor(ticker: Ticker) {
    ticker.add((t) => {
      this.now += t.deltaMS;
      for (const j of [...this.jobs]) {
        const k = Math.min(1, (this.now - j.at) / j.ms);
        j.set(j.from + (j.to - j.from) * j.ease(k));
        if (k >= 1) {
          this.jobs.splice(this.jobs.indexOf(j), 1);
          j.done();
        }
      }
    });
  }

  to(from: number, to: number, ms: number, set: (v: number) => void, e: Ease = ease.outCubic): Promise<void> {
    return new Promise((done) => this.jobs.push({ from, to, ms: Math.max(1, ms), at: this.now, ease: e, set, done }));
  }

  wait(ms: number): Promise<void> {
    return this.to(0, 1, ms, () => {});
  }
}
