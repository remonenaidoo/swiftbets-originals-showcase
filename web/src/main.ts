import 'pixi.js/unsafe-eval'; // PixiJS without eval, so the page runs under a strict CSP.
import { api, ApiError, describe, rand, type GameInfo, type SpinView, type Sym } from './api';
import { sound } from './audio';
import { Scene } from './scene';
import { close, open, renderHistory, renderRules } from './sheets';
import { ease } from './tweens';

const $ = (id: string) => document.getElementById(id)!;
const scene = new Scene();
let info!: GameInfo;
let balance: number | null = null;
let stakeIndex = 0;
let busy = false;
let turbo = false;
let auto: { left: number; loss: number; start: number } | null = null;
const started = Date.now();

const stake = () => info.stakes[stakeIndex];
const sleep = (ms: number) => scene.tweens.wait(ms);
const windowOf = (strips: Sym[][], stops: number[]) =>
  strips.map((strip, reel) => [0, 1, 2].map((row) => strip[(stops[reel] + row) % strip.length]));

function setBalance(v: number | null) {
  if (v !== null) balance = v;
  $('balance').textContent = balance === null ? '…' : rand(balance);
}

function setWin(v: number, animate = false) {
  const el = $('win');
  if (!animate || v === 0) { el.textContent = rand(v); return Promise.resolve(); }
  return scene.tweens.to(0, v, turbo ? 300 : Math.min(2200, 500 + v / 4), (x) => (el.textContent = rand(Math.round(x))), ease.outCubic);
}

function refreshControls() {
  ($('stake-down') as HTMLButtonElement).disabled = busy || !!auto || stakeIndex === 0;
  ($('stake-up') as HTMLButtonElement).disabled = busy || !!auto || stakeIndex === info.stakes.length - 1;
  $('stake').textContent = rand(stake());
  const spin = $('spin') as HTMLButtonElement;
  spin.classList.toggle('stop', !!auto);
  spin.firstChild!.textContent = auto ? 'STOP' : 'SPIN';
  spin.disabled = busy && !auto;
  const count = $('auto-count');
  count.style.display = auto ? 'block' : 'none';
  count.textContent = auto ? String(auto.left) : '';
  $('btn-turbo').classList.toggle('on', turbo);
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
  const done = overlay(label, rand(0), `${x.toFixed(1)}× stake`, '', turbo ? 1600 : 3600);
  void scene.tweens.to(0, win, turbo ? 900 : 2400, (v) => ($('ov-amount').textContent = rand(Math.round(v))), ease.outCubic);
  await done;
}

/** Shows what the server decided: winning lines, scatters, the win count-up and any celebration. */
async function present(view: SpinView, strips: Sym[][], st: number) {
  const win = windowOf(strips, view.stops);
  const scatterCells: [number, number][] = [];
  win.forEach((col, reel) => col.forEach((s, row) => { if (s === 'Scatter') scatterCells.push([reel, row]); }));
  const scatterPays = view.scatters >= 3;
  if (view.win > 0 || scatterPays) {
    sound.win(view.win / st);
    if (view.win > 0 && view.win < st * 10) scene.coins(Math.min(40, 6 + Math.round((view.win / st) * 6)));
    const shown = scene.showWins(info, view.lines, scatterPays ? scatterCells : [], turbo);
    await Promise.all([shown, setWin(view.win, true)]);
    await bigWin(view.win, st);
  } else {
    setWin(0);
  }
}

function spinReels() {
  sound.spin();
  scene.clearWins();
  scene.reels.spin(turbo);
}

async function landReels(stops: number[], strips: Sym[][]) {
  await scene.reels.stop(stops, strips, turbo, (reel, tease) => {
    sound.stop(reel);
    const col = windowOf(strips, stops)[reel];
    if (col.includes('Scatter')) sound.scatter(reel);
    if (tease) sound.tease();
  });
}

/** Lands the reels somewhere harmless after a failed request, so nothing looks decided. */
async function abortSpin(strips: Sym[][]) {
  await landReels(strips.map((s) => Math.floor(Math.random() * s.length)), strips);
  scene.clearWins();
}

async function spinOnce(): Promise<boolean> {
  busy = true;
  refreshControls();
  setWin(0);
  spinReels();
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
    if (e instanceof ApiError && e.code === 'feature_in_progress') {
      await resumeFeature();
    } else {
      toast(describe(e));
    }
    busy = false;
    refreshControls();
    return false;
  }
  await landReels(view.stops, info.baseReels);
  setBalance(view.balance);
  await present(view, info.baseReels, stake());
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
  await overlay(`${left} FREE SPINS`, '', `Every win ×${info.freeSpinMultiplier}`, 'START', auto ? 2500 : undefined);
  scene.setFeatureMood(true);
  scene.reels.setStrips(info.freeReels, view.stops);
  banner.style.display = 'flex';
  const position = () => { banner.style.top = `${Math.round(scene.layout().boardBottom + 10)}px`; };
  position();
  window.addEventListener('resize', position);
  while (left > 0) {
    $('fs-left').textContent = String(left);
    $('fs-won').textContent = rand(won);
    await sleep(turbo ? 150 : 450);
    spinReels();
    let next: SpinView;
    try {
      next = await api.next(view.roundId);
    } catch (e) {
      await abortSpin(info.freeReels);
      toast(`${describe(e)} Your free spins are saved; reopen the game to continue.`);
      interrupted = true;
      break;
    }
    await landReels(next.stops, info.freeReels);
    setBalance(next.balance);
    await present(next, info.freeReels, st);
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
    await sleep(turbo ? 150 : 350);
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
    if (!busy) void spinOnce();
  });
  window.addEventListener('keydown', (e) => {
    if (e.code === 'Space' && !e.repeat && !document.querySelector('.sheet[style*="block"]')) { e.preventDefault(); $('spin').click(); }
  });
  $('stake-down').addEventListener('click', () => { if (stakeIndex > 0) { stakeIndex--; refreshControls(); } });
  $('stake-up').addEventListener('click', () => { if (stakeIndex < info.stakes.length - 1) { stakeIndex++; refreshControls(); } });
  $('btn-turbo').addEventListener('click', () => { turbo = !turbo; refreshControls(); });
  $('btn-sound').addEventListener('click', () => { sound.toggle(); refreshControls(); });
  $('btn-rules').addEventListener('click', () => { renderRules(info, stake()); open('sheet-rules'); });
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
  const [state] = await Promise.all([api.state(), scene.init($('game'), (p) => (bar.style.width = `${Math.round(p * 100)}%`))]);
  info = state.info;
  $('rtp').textContent = `RTP ${info.rtp.toFixed(2)}%`;
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
