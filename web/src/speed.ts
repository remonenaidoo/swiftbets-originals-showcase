/** Spin speed levels. The outcome is the server's either way; speed only changes how long it takes to show. */
export interface Speed {
  name: string;
  reelSpeed: number; // cells per second while spinning
  startGap: number; // ms between reels starting
  minSpin: number; // ms the reels spin at least, so a fast reply still looks like a spin
  stopGap: number; // ms between reels stopping
  land: number; // ms of the bounce as a reel lands
  tease: number; // extra ms on a reel that could complete three Scatters
  pulse: number; // ms of the winning-symbol pulse
  countUp: (win: number) => number; // ms for the win to count up (win in cents)
  bigWin: number; // ms a big-win card stays up during autoplay or skipped play
  between: number; // ms between autoplay spins
}

export const SPEEDS: Speed[] = [
  { name: 'Normal', reelSpeed: 28, startGap: 50, minSpin: 450, stopGap: 140, land: 210, tease: 750, pulse: 440,
    countUp: (w) => Math.min(1500, 350 + w / 5), bigWin: 3200, between: 280 },
  { name: 'Fast', reelSpeed: 34, startGap: 20, minSpin: 180, stopGap: 60, land: 150, tease: 380, pulse: 260,
    countUp: () => 300, bigWin: 1800, between: 120 },
  { name: 'Turbo', reelSpeed: 40, startGap: 0, minSpin: 0, stopGap: 0, land: 120, tease: 0, pulse: 160,
    countUp: () => 0, bigWin: 1100, between: 40 },
];

const KEY = 'gs.speed';

export function savedSpeed(): number {
  try {
    const v = Number(localStorage.getItem(KEY));
    return Number.isInteger(v) && v >= 0 && v < SPEEDS.length ? v : 0;
  } catch {
    return 0;
  }
}

export function saveSpeed(level: number) {
  try { localStorage.setItem(KEY, String(level)); } catch { /* storage blocked: the level just isn't remembered */ }
}
