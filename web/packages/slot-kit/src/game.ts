import 'pixi.js/unsafe-eval'; // PixiJS without eval, so the page runs under a strict CSP.
import { apiFor, ApiError, describe, rand, type GameInfo, type LockedCell, type SpinView, type Sym } from './api';
import { sound } from './audio';
import type { SlotConfig } from './config';
import { HUD } from './hud';
import { Scene } from './scene';
import { close, open, renderHistory, renderRules, wireSheets } from './sheets';
import { SPEEDS, savedSpeed, saveSpeed } from './speed';
import { ease } from './tweens';

/** Starts a slot: mounts the HUD, loads the game's state and art, and plays. Everything game-specific comes from `config`. */
export function startSlot(config: SlotConfig) {
document.body.insertAdjacentHTML('afterbegin', HUD(config.title));
for (const [k, v] of Object.entries(config.css ?? {})) document.documentElement.style.setProperty(k, v);
wireSheets();
const api = apiFor(config.gameId);
const $ = (id: string) => document.getElementById(id)!;
const scene = new Scene(config);
let info!: GameInfo;
let images: Record<string, string> = {};
let balance: number | null = null;
let stakeIndex = 0;
let busy = false;
let speedLevel = savedSpeed();
let quick = false; // the player tapped to stop this spin: show the rest of it at Turbo speed
let auto: { left: number; loss: number; start: number } | null = null;
const started = Date.now();

const stake = () => info.stakes[stakeIndex];
const sp = () => SPEEDS[quick ? SPEEDS.length - 1 : speedLevel];
const sleep = (ms: number) => scene.tweens.wait(ms);
const windowOf = (strips: Sym[][], stops: number[], locked: LockedCell[] = []) =>
  strips.map((strip, reel) => [0, 1, 2].map((row) => locked.find((l) => l.reel === reel && l.row === row)?.symbol ?? strip[(stops[reel] + row) % strip.length]));
const isScatter = (s: Sym) => info.symbols.find((x) => x.name === s)?.kind === 'Scatter';

/** The balance as the wallet last reported it (or will once the bet in flight lands). */
function setBalance(v: number | null) {
  if (v !== null) balance = v;
  $('balance').textContent = balance === null ? '…' : rand(balance);
}

/** Counts the win up; the balance counts up with it, from `balanceBefore` to `balanceBefore + v`. */
function setWin(v: number, animate = false, balanceBefore: number | null = null) {
  const el = $('win');
  const ms = sp().countUp(v);
  if (!animate || v === 0 || ms === 0) {
    el.textContent = rand(v);
    if (balanceBefore !== null) setBalance(balanceBefore + v);
    return Promise.resolve();
  }
  return scene.tweens.to(0, v, ms, (x) => {
    const shown = Math.round(x);
    el.textContent = rand(shown);
    if (balanceBefore !== null) $('balance').textContent = rand(balanceBefore + shown);
  }, ease.outCubic).then(() => { if (balanceBefore !== null) setBalance(balanceBefore + v); });
}

/** After a failed or uncertain request, show what the wallet really holds. */
async function refreshBalance(fallback: number | null) {
  try { setBalance((await api.state()).balance); } catch { setBalance(fallback); }
}

function refreshControls() {
  document.body.dataset.busy = String(busy);
  ($('stake-down') as HTMLButtonElement).disabled = busy || !!auto || stakeIndex === 0;
  ($('stake-up') as HTMLButtonElement).disabled = busy || !!auto || stakeIndex === info.stakes.length - 1;
  $('stake').textContent = rand(stake());
  const spin = $('spin') as HTMLButtonElement;
  spin.classList.toggle('stop', !!auto);
  spin.firstChild!.textContent = auto ? 'STOP' : 'SPIN';
  spin.disabled = busy && !auto && !scene.reels.spinning;
  const count = $('auto-count');
  count.style.display = auto ? 'block' : 'none';
  count.textContent = auto ? String(auto.left) : '';
  const turbo = $('btn-turbo');
  turbo.classList.toggle('on', speedLevel > 0);
  turbo.title = `Spin speed: ${SPEEDS[speedLevel].name}`;
  $('speed-level').textContent = speedLevel > 0 ? SPEEDS[speedLevel].name.toUpperCase() : '';
  $('speed-level').style.display = speedLevel > 0 ? 'block' : 'none';
  $('btn-sound').textContent = sound.muted ? '🔇' : '🔊';
}

let toastTimer = 0;
function toast(text: string) {
  const el = $('toast');
  el.textContent = text;
  el.style.display = 'block';
  clearTimeout(toastTimer);
  toastTimer = window.setTimeout(() => (el.style.display = 'none'), 3800);
}

/** A full-screen card (big win, free spins intro and total). Resolves on tap, or by itself during autoplay. */
function overlay(kicker: string, amount: string, sub: string, button: string, autoMs?: number): Promise<void> {
  $('ov-kicker').textContent = kicker;
  $('ov-amount').textContent = amount;
  $('ov-sub').textContent = sub;
  const go = $('ov-go');
  go.textContent = button;
  go.style.display = button ? 'inline-block' : 'none';
  const el = $('overlay');
  el.style.display = 'grid';
  return new Promise((done) => {
    let finished = false;
    const finish = () => { if (finished) return; finished = true; el.style.display = 'none'; el.onclick = null; done(); };
    el.onclick = finish;
    if (autoMs) setTimeout(finish, autoMs);
  });
}

async function bigWin(win: number, st: number) {
  const x = win / st;
  if (x < 10) return;
  const label = x >= 50 ? 'EPIC WIN' : x >= 25 ? 'MEGA WIN' : 'BIG WIN';
  sound.big();
  scene.coins(x >= 50 ? 160 : x >= 25 ? 100 : 60);
  const done = overlay(label, rand(0), `${x.toFixed(1)}× stake`, '', sp().bigWin);
  void scene.tweens.to(0, win, sp().bigWin * 0.65, (v) => ($('ov-amount').textContent = rand(Math.round(v))), ease.outCubic);
  await done;
}

/** Shows what the server decided: winning lines, scatters, the win count-up and any celebration. */
async function present(view: SpinView, strips: Sym[][], st: number, balanceBefore: number | null, inFeature = false) {
  const win = windowOf(strips, view.stops, view.locked);
  const scatterCells: [number, number][] = [];
  win.forEach((col, reel) => col.forEach((s, row) => { if (isScatter(s)) scatterCells.push([reel, row]); }));
  const scatterPays = view.scatters >= 3;
  if (view.win > 0 || scatterPays) {
    sound.win(view.win / st);
    if (view.win > 0 && view.win < st * 10) scene.coins(Math.min(40, 6 + Math.round((view.win / st) * 6)));
    const shown = scene.showWins(info, view.lines, scatterPays ? scatterCells : [], sp().pulse);
    if (view.win > 0) scene.showAmount(rand(view.win));
    await Promise.all([shown, setWin(view.win, true, balanceBefore)]);
    // With locks nearly every free spin is big; a locking feature celebrates once, with its total.
    if (!(inFeature && info.lockSplits)) await bigWin(view.win, st);
  } else {
    setWin(0);
    setBalance(view.balance);
  }
}

function spinReels() {
  sound.spin();
  scene.clearWins();
  scene.reels.spin(sp());
}

async function landReels(stops: number[], strips: Sym[][]) {
  await scene.reels.stop(stops, strips, sp(), (reel, tease) => {
    sound.stop(reel);
    const col = windowOf(strips, stops)[reel];
    col.forEach((sym, row) => { if (isScatter(sym)) scene.scatterLanded(reel, row); });
    if (col.some(isScatter)) sound.scatter(reel);
    scene.tease(tease ? reel + 1 : null);
    if (tease) sound.tease();
  });
}

/** Lands the reels somewhere harmless after a failed request, so nothing looks decided. */
async function abortSpin(strips: Sym[][]) {
  await landReels(strips.map((s) => Math.floor(Math.random() * s.length)), strips);
  scene.clearWins();
}

/** Waits out the rest of the minimum spin time, unless the player taps to stop. */
async function minSpin(since: number) {
  const left = sp().minSpin - (performance.now() - since);
  if (left > 0 && !quick) await Promise.race([sleep(left), new Promise<void>((r) => (hurry = r))]);
  hurry = null;
}
let hurry: (() => void) | null = null;

/** The amount a reply's balance had before its win was added: what to show while the reels land. */
const beforeWin = (v: SpinView) => (v.balance === null ? null : v.balance - v.win);

async function spinOnce(): Promise<boolean> {
  busy = true;
  quick = false;
  const before = balance;
  // The stake leaves the balance the moment Spin is pressed; the wallet's reply confirms it.
  if (balance !== null) setBalance(balance - stake());
  setWin(0);
  spinReels();
  refreshControls();
  const since = performance.now();
  let view: SpinView;
  const key = crypto.randomUUID();
  try {
    try {
      view = await api.spin(stake(), key);
    } catch (e) {
      // A dropped connection may have lost the reply, not the spin: ask again with the same key (the server returns the same round).
      if (!(e instanceof ApiError) || e.code !== 'network') throw e;
      await sleep(1200);
      view = await api.spin(stake(), key);
    }
  } catch (e) {
    await abortSpin(info.baseReels);
    await refreshBalance(before);
    if (e instanceof ApiError && e.code === 'feature_in_progress') {
      await resumeFeature();
    } else {
      toast(describe(e));
    }
    busy = false;
    refreshControls();
    return false;
  }
  setBalance(beforeWin(view));
  await minSpin(since);
  await landReels(view.stops, info.baseReels);
  refreshControls();
  await present(view, info.baseReels, stake(), beforeWin(view));
  setBalance(view.balance);
  if (view.featureTriggered && view.freeSpinsLeft > 0) await playFeature(view, stake(), view.win);
  busy = false;
  refreshControls();
  return true;
}

/** Free spins: an intro, each spin played on the server, retriggers, and the total at the end. */
async function playFeature(view: SpinView, st: number, alreadyWon: number) {
  const banner = $('feature');
  let left = view.freeSpinsLeft;
  let won = 0;
  let interrupted = false;
  sound.feature();
  scene.coins(50);
  const sub = info.lockSplits ? 'Doubles and triples lock in place' : `Every win ×${info.freeSpinMultiplier}`;
  await overlay(`${left} FREE SPINS`, '', sub, 'START', auto ? 2500 : undefined);
  scene.setFeatureMood(true);
  // Spin 0's stops are on the base reels; a resumed feature's last stops are on the free reels.
  scene.reels.setStrips(info.freeReels, view.seq === 0 ? info.freeReels.map((s) => Math.floor(Math.random() * s.length)) : view.stops);
  scene.setLocks(view.seq === 0 ? [] : view.locked);
  banner.style.display = 'flex';
  const position = () => { banner.style.top = `${Math.round(scene.layout().boardBottom + 10)}px`; };
  position();
  window.addEventListener('resize', position);
  while (left > 0) {
    $('fs-left').textContent = String(left);
    $('fs-won').textContent = rand(won);
    await sleep(sp().between + 120);
    quick = false;
    spinReels();
    refreshControls();
    const since = performance.now();
    let next: SpinView;
    try {
      next = await api.next(view.roundId);
    } catch (e) {
      await abortSpin(info.freeReels);
      toast(`${describe(e)} Your free spins are saved; reopen the game to continue.`);
      interrupted = true;
      break;
    }
    setBalance(beforeWin(next));
    await minSpin(since);
    await landReels(next.stops, info.freeReels);
    scene.setLocks(next.locked);
    refreshControls();
    await present(next, info.freeReels, st, beforeWin(next), true);
    setBalance(next.balance);
    won += next.win;
    $('fs-won').textContent = rand(won);
    if (next.featureTriggered) {
      sound.feature();
      await overlay(`+${info.freeSpins} FREE SPINS`, '', 'Retriggered!', '', 1800);
    }
    if (next.capReached) await overlay('MAX WIN', rand(next.roundPaid), `${info.maxWinStakes.toLocaleString('en-ZA')}× stake reached`, 'COLLECT', auto ? 3000 : undefined);
    left = next.freeSpinsLeft;
  }
  banner.style.display = 'none';
  window.removeEventListener('resize', position);
  scene.setFeatureMood(false);
  scene.clearWins();
  scene.setLocks([]);
  if (interrupted) {
    scene.reels.setStrips(info.baseReels, info.baseReels.map((s) => Math.floor(Math.random() * s.length)));
    return;
  }
  const total = alreadyWon + won;
  scene.coins(80);
  sound.big();
  await overlay('FREE SPINS WON', rand(total), `${(total / st).toFixed(1)}× stake`, 'COLLECT', auto ? 3000 : undefined);
  setWin(total);
  scene.reels.setStrips(info.baseReels, info.baseReels.map((s) => Math.floor(Math.random() * s.length)));
}

async function runAuto() {
  while (auto && auto.left > 0) {
    if (balance !== null && balance < stake()) { toast('Not enough balance for this stake. Autoplay stopped.'); break; }
    if (balance !== null && auto.start - balance >= auto.loss) { toast('Loss limit reached. Autoplay stopped.'); break; }
    const ok = await spinOnce();
    if (!ok || !auto) break;
    auto.left--;
    refreshControls();
    await sleep(sp().between);
  }
  auto = null;
  refreshControls();
}

function setupAutoplay() {
  let spins = 25;
  let loss = 25;
  const spinChoices = [10, 25, 50, 100];
  const lossChoices = [10, 25, 50, 100];
  const draw = () => {
    $('auto-spins').innerHTML = spinChoices.map((n) => `<button class="${n === spins ? 'on' : ''}" data-n="${n}">${n}</button>`).join('');
    $('auto-loss').innerHTML = lossChoices.map((n) => `<button class="${n === loss ? 'on' : ''}" data-l="${n}">${rand(n * stake())}</button>`).join('');
  };
  $('auto-spins').addEventListener('click', (e) => { const n = (e.target as HTMLElement).dataset.n; if (n) { spins = +n; draw(); } });
  $('auto-loss').addEventListener('click', (e) => { const l = (e.target as HTMLElement).dataset.l; if (l) { loss = +l; draw(); } });
  $('btn-auto').addEventListener('click', () => { if (busy || auto) return; draw(); open('sheet-auto'); });
  $('auto-start').addEventListener('click', () => {
    close('sheet-auto');
    auto = { left: spins, loss: loss * stake(), start: balance ?? 0 };
    refreshControls();
    void runAuto();
  });
}

function setupControls() {
  $('spin').addEventListener('click', () => {
    sound.unlock();
    if (auto) { auto = null; refreshControls(); return; }
    if (!busy) { void spinOnce(); return; }
    // Tap while the reels turn: stop them now (the result is the server's either way).
    if (scene.reels.spinning) { quick = true; hurry?.(); scene.reels.slam(); }
  });
  window.addEventListener('keydown', (e) => {
    if (e.code === 'Space' && !e.repeat && !document.querySelector('.sheet[style*="block"]')) { e.preventDefault(); $('spin').click(); }
  });
  $('stake-down').addEventListener('click', () => { if (stakeIndex > 0) { stakeIndex--; refreshControls(); } });
  $('stake-up').addEventListener('click', () => { if (stakeIndex < info.stakes.length - 1) { stakeIndex++; refreshControls(); } });
  $('btn-turbo').addEventListener('click', () => {
    speedLevel = (speedLevel + 1) % SPEEDS.length;
    saveSpeed(speedLevel);
    refreshControls();
    toast(`Spin speed: ${SPEEDS[speedLevel].name}`);
  });
  // Coming back to the game (another tab, the site, a deposit): show the wallet as it is now.
  document.addEventListener('visibilitychange', () => { if (!document.hidden && !busy) void refreshBalance(balance); });
  $('btn-sound').addEventListener('click', () => { sound.toggle(); refreshControls(); });
  $('btn-rules').addEventListener('click', () => { renderRules(config, info, stake(), images); open('sheet-rules'); });
  $('btn-history').addEventListener('click', async () => {
    renderHistory([]);
    $('history-body').innerHTML = '<p>Loading…</p>';
    open('sheet-history');
    try { renderHistory((await api.state()).recent); } catch (e) { $('history-body').innerHTML = `<p>${describe(e)}</p>`; }
  });
  setupAutoplay();
  setInterval(() => {
    const s = Math.floor((Date.now() - started) / 1000);
    const hh = Math.floor(s / 3600), mm = Math.floor((s % 3600) / 60), ss = s % 60;
    $('clock').textContent = `${hh ? `${hh}:` : ''}${String(mm).padStart(2, '0')}:${String(ss).padStart(2, '0')}`;
  }, 1000);
}

/** Plays the player's unfinished free spins, at the stake of the spin that triggered them. */
async function playOpenFeature(state: { openFeature: SpinView | null; recent: { roundId: string; stake: number }[] }) {
  const open = state.openFeature;
  if (!open) return;
  const st = state.recent.find((r) => r.roundId === open.roundId)?.stake ?? stake();
  await playFeature(open, st, open.roundPaid);
}

/** The server says free spins are unfinished (e.g. a reply was lost): fetch them and play them. */
async function resumeFeature() {
  try {
    const state = await api.state();
    if (state.openFeature) {
      toast('You have free spins to finish first.');
      await playOpenFeature(state);
    }
  } catch (e) {
    toast(describe(e));
  }
}

async function boot() {
  const bar = document.querySelector<HTMLElement>('#loader .bar i')!;
  if (!api.hasSession()) {
    document.querySelector('#loader .studio')!.textContent = 'Open this game from the SwiftBets casino lobby.';
    return;
  }
  const state = await api.state();
  info = state.info;
  await scene.init($('game'), info, (p) => (bar.style.width = `${Math.round(p * 100)}%`));
  scene.reels.scatter = isScatter;
  $('fs-mult').textContent = `×${info.freeSpinMultiplier}`;
  $('fs-mult').style.display = info.freeSpinMultiplier > 1 ? '' : 'none';
  void Promise.all(Object.entries(scene.textures).map(async ([name, t]) => [name, await scene.app.renderer.extract.base64(t)] as const))
    .then((pairs) => (images = Object.fromEntries(pairs)));
  setBalance(state.balance);
  scene.reels.setStrips(info.baseReels, info.baseReels.map((s) => Math.floor(Math.random() * s.length)));
  scene.layout();
  setupControls();
  refreshControls();
  const loader = $('loader');
  loader.style.opacity = '0';
  setTimeout(() => loader.remove(), 500);
  if (state.openFeature) {
    busy = true;
    refreshControls();
    toast('Welcome back. Your free spins are waiting.');
    await playOpenFeature(state);
    busy = false;
    refreshControls();
  }
}

boot().catch((e) => {
  document.querySelector('#loader .studio')!.textContent = describe(e);
});
}
