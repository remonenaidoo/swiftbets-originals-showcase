// Sound, synthesised with WebAudio so the game ships no audio files. Muted state is remembered per device.
const KEY = 'gs-muted';
let muted = (() => { try { return localStorage.getItem(KEY) === '1'; } catch { return false; } })();
let ctx: AudioContext | null = null;

const ac = () => (ctx ??= new AudioContext());

function tone(freq: number, ms: number, type: OscillatorType = 'sine', gain = 0.08, at = 0, slide = 0) {
  if (muted) return;
  const c = ac();
  const t = c.currentTime + at / 1000;
  const o = c.createOscillator();
  const g = c.createGain();
  o.type = type;
  o.frequency.setValueAtTime(freq, t);
  if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(30, freq + slide), t + ms / 1000);
  g.gain.setValueAtTime(gain, t);
  g.gain.exponentialRampToValueAtTime(0.0001, t + ms / 1000);
  o.connect(g).connect(c.destination);
  o.start(t);
  o.stop(t + ms / 1000 + 0.02);
}

export const sound = {
  get muted() { return muted; },
  toggle() {
    muted = !muted;
    try { localStorage.setItem(KEY, muted ? '1' : '0'); } catch { /* private mode */ }
    if (!muted) void ac().resume();
    return muted;
  },
  unlock() { if (!muted) void ac().resume(); },
  spin() { tone(220, 120, 'triangle', 0.05, 0, 180); },
  stop(i: number) { tone(140 + i * 18, 90, 'square', 0.035); tone(70, 140, 'sine', 0.08); },
  tease() { tone(330, 600, 'sawtooth', 0.025, 0, 330); },
  scatter(n: number) { tone(660 + n * 120, 260, 'triangle', 0.07); tone(990 + n * 120, 260, 'sine', 0.05, 60); },
  win(size: number) {
    const notes = size >= 3 ? [523, 659, 784, 1047, 1319] : size >= 1 ? [523, 659, 784] : [659, 784];
    notes.forEach((f, i) => tone(f, 220, 'triangle', 0.06, i * 90));
  },
  big() { [392, 523, 659, 784, 1047, 784, 1047, 1319].forEach((f, i) => tone(f, 320, 'square', 0.04, i * 140)); },
  tick() { tone(1800, 30, 'square', 0.012); },
  feature() { [262, 330, 392, 523, 659, 784, 1047].forEach((f, i) => tone(f, 400, 'triangle', 0.06, i * 110)); },
};
