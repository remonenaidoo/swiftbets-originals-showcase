import 'pixi.js/unsafe-eval'; // PixiJS without eval, so the page runs under a strict CSP.
import { HUD, imageArt, rand, Scene, SPEEDS, savedSpeed, saveSpeed, sound, wireSheets, open, close, renderHistory, type GameInfo, type SlotConfig } from '@originals/slot-kit';
import { call, describe, key, query, ApiError, type RoundSummary, type Seeds } from './api';
import { seedsHtml } from './seeds';

/**
 * A slot whose whole round is settled by the server in one request (Cape Point Ways, Highveld Hold): the page plays
 * the stored round back on the slot kit's reels. One bet and one win per round, so the balance shown is the wallet's.
 */
export interface RoundSlotConfig {
  gameId: string;
  title: string;
  mechanic: 'ways' | 'hold';
  /** Symbol name → asset path (under /originals/). */
  symbols: Record<string, string>;
  background: { portrait: string; landscape: string };
  logo: string;
  colours: SlotConfig['colours'];
  css?: Record<string, string>;
  /** The rules sheet for this stake; `img` gives a symbol's picture. */
  rules: (info: Record<string, any>, stake: number, img: (symbol: string) => string) => string;
}

type Cell = [number, number];
interface WaysWin { symbol: string; reels: number; ways: number; units: number; cells: Cell[] }
interface WaysSpin { free: boolean; stops: number[]; wins: WaysWin[]; scatters: number; scatterUnits: number; units: number; paid: number }
interface HoldLine { line: number; symbol: string; count: number; units: number; cells: Cell[] }
interface Coin { cell: Cell; stakes: number }
interface Respin { added: Coin[]; left: number }
interface WaysDetail { spins: WaysSpin[]; units: number; capped: boolean; freeSpins: number }
interface HoldDetail { stops: number[]; lines: HoldLine[]; coins: Coin[]; respins: Respin[]; featureUnits: number; grand: boolean; units: number; capped: boolean }
interface PlayView<D> { roundId: string; multiplierBp: number; win: number; detail: D; balance: number | null }
interface State { balance: number | null; recent: RoundSummary[]; seeds: Seeds; info: { stakes: number[]; rtp: number; maxWinStakes: number; name: string; rules: Record<string, any> } }

const JACKPOTS: Record<number, string> = { 15: 'MINI', 40: 'MINOR', 150: 'MAJOR' };

export async function startRoundSlot(cfg: RoundSlotConfig) {
  document.body.insertAdjacentHTML('afterbegin', HUD(cfg.title));
  for (const [k, v] of Object.entries(cfg.css ?? {})) document.documentElement.style.setProperty(k, v);
  wireSheets();
  const $ = (id: string) => document.getElementById(id)!;
  // This page has a fairness sheet too: the hud's history button opens history; the rules sheet carries the seeds.
  let state: State;
  try {
    state = await call<State>(query(`instant/${cfg.gameId}/state`));
  } catch (e) {
    $('loader').innerHTML = `<p style="color:#eee;padding:24px;text-align:center">${describe(e)}</p>`;
    return;
  }
  const info = state.info.rules;
  const unitsPerStake = info.units as number;
  const symbols = Object.keys(cfg.symbols);
  const fakeInfo = { symbols: symbols.map((name) => ({ name, kind: name.startsWith('Wild') ? 'Wild' : name === 'Pearl' || name === 'Coin' ? 'Scatter' : 'Regular', weight: 1, paysAs: name })) } as unknown as GameInfo;
  const scene = new Scene({ gameId: cfg.gameId, title: cfg.title, art: imageArt(cfg.symbols), background: cfg.background, logo: cfg.logo, colours: cfg.colours, rules: () => '' });
  await scene.init($('game'), fakeInfo, (p) => { ($('loader').querySelector('.bar i') as HTMLElement).style.width = `${Math.round(p * 100)}%`; });
  $('loader').style.opacity = '0';
  setTimeout(() => $('loader').remove(), 500);

  const baseStrips: string[][] = cfg.mechanic === 'ways' ? info.baseReels : info.reels;
  const freeStrips: string[][] = cfg.mechanic === 'ways' ? info.freeReels : Array.from({ length: 5 }, () => ['Blank', 'Blank', 'Blank', 'Blank']);
  const randomStops = (strips: string[][]) => strips.map((s) => Math.floor(Math.random() * s.length));
  scene.reels.setStrips(baseStrips, randomStops(baseStrips));
  scene.reels.scatter = (s) => s === 'Pearl';
  scene.layout();
  $('rtp').textContent = `RTP ${state.info.rtp.toFixed(2)}%`;

  let balance = state.balance, stakeIndex = Math.max(0, state.info.stakes.indexOf(Number((() => { try { return localStorage.getItem(`stake:${cfg.gameId}`); } catch { return null; } })())));
  let busy = false, speedLevel = savedSpeed(), auto: { left: number; loss: number; start: number } | null = null;
  const stake = () => state.info.stakes[stakeIndex];
  const sp = () => SPEEDS[speedLevel];
  const sleep = (ms: number) => scene.tweens.wait(ms);
  const money = (units: number, st: number) => Math.floor(units * st / unitsPerStake);

  const setBalance = (v: number | null) => { if (v !== null) balance = v; $('balance').textContent = balance === null ? '…' : rand(balance); };
  const setWin = (v: number) => { $('win').textContent = rand(v); };
  let toastTimer = 0;
  const toast = (t: string) => { const el = $('toast'); el.textContent = t; el.style.display = 'block'; clearTimeout(toastTimer); toastTimer = window.setTimeout(() => (el.style.display = 'none'), 3800); };

  const overlay = (kicker: string, amount: string, sub: string, button: string, autoMs?: number) => new Promise<void>((done) => {
    $('ov-kicker').textContent = kicker; $('ov-amount').textContent = amount; $('ov-sub').textContent = sub;
    const go = $('ov-go'); go.textContent = button; go.style.display = button ? 'inline-block' : 'none';
    const el = $('overlay'); el.style.display = 'grid';
    let finished = false;
    const finish = () => { if (finished) return; finished = true; el.style.display = 'none'; el.onclick = null; done(); };
    el.onclick = finish;
    if (autoMs) setTimeout(finish, autoMs);
  });

  const bigWin = async (win: number, st: number) => {
    const x = win / st;
    if (x < 10) return;
    sound.big();
    scene.coins(x >= 50 ? 160 : x >= 25 ? 100 : 60);
    const done = overlay(x >= 50 ? 'EPIC WIN' : x >= 25 ? 'MEGA WIN' : 'BIG WIN', rand(0), `${x.toFixed(1)}× stake`, '', sp().bigWin);
    void scene.tweens.to(0, win, sp().bigWin * 0.65, (v) => ($('ov-amount').textContent = rand(Math.round(v))));
    await done;
  };

  const refreshControls = () => {
    document.body.dataset.busy = String(busy);
    ($('stake-down') as HTMLButtonElement).disabled = busy || !!auto || stakeIndex === 0;
    ($('stake-up') as HTMLButtonElement).disabled = busy || !!auto || stakeIndex === state.info.stakes.length - 1;
    $('stake').textContent = rand(stake());
    const spin = $('spin') as HTMLButtonElement;
    spin.classList.toggle('stop', !!auto);
    spin.firstChild!.textContent = auto ? 'STOP' : 'SPIN';
    spin.disabled = busy && !auto;
    $('auto-count').style.display = auto ? 'block' : 'none';
    $('auto-count').textContent = auto ? String(auto.left) : '';
    $('btn-turbo').classList.toggle('on', speedLevel > 0);
    $('speed-level').textContent = speedLevel > 0 ? SPEEDS[speedLevel].name.toUpperCase() : '';
    $('speed-level').style.display = speedLevel > 0 ? 'block' : 'none';
    $('btn-sound').textContent = sound.muted ? '🔇' : '🔊';
  };

  const images: Record<string, string> = Object.fromEntries(Object.entries(cfg.symbols).map(([k, v]) => [k, `${import.meta.env.BASE_URL}${v}`]));
  const rules = () => {
    $('rules-body').innerHTML = cfg.rules(info, stake(), (s) => `<img src="${images[s] ?? ''}" alt="${s}">`) + `<h3>Fairness</h3>${seedsHtml(state.seeds)}`;
    const rotate = document.getElementById('rotate');
    if (rotate) rotate.onclick = async () => {
      try { state.seeds = await call<Seeds>('instant/seed', { clientSeed: (document.getElementById('client-seed') as HTMLInputElement).value.trim() }); rules(); } catch (e) { toast(describe(e)); }
    };
  };
  const refresh = async () => {
    try { const s = await call<State>(query(`instant/${cfg.gameId}/state`)); state.seeds = s.seeds; renderHistory(s.recent); } catch { /* next round */ }
  };

  $('stake-down').onclick = () => { stakeIndex = Math.max(0, stakeIndex - 1); saveStake(); refreshControls(); };
  $('stake-up').onclick = () => { stakeIndex = Math.min(state.info.stakes.length - 1, stakeIndex + 1); saveStake(); refreshControls(); };
  const saveStake = () => { try { localStorage.setItem(`stake:${cfg.gameId}`, String(stake())); } catch { /* private mode */ } };
  $('btn-sound').onclick = () => { sound.toggle(); refreshControls(); };
  $('btn-turbo').onclick = () => { speedLevel = (speedLevel + 1) % SPEEDS.length; saveSpeed(speedLevel); refreshControls(); };
  $('btn-rules').onclick = () => { rules(); open('sheet-rules'); };
  $('btn-history').onclick = () => { renderHistory(state.recent); open('sheet-history'); void refresh(); };
  document.addEventListener('pointerdown', () => sound.unlock(), { once: true });

  // Autoplay: a number of spins, stopped by a loss limit, an error or Stop.
  const choices = (id: string, values: number[], label: (v: number) => string, on: number) => {
    $(id).innerHTML = values.map((v) => `<button data-v="${v}" class="${v === on ? 'on' : ''}">${label(v)}</button>`).join('');
    for (const b of $(id).querySelectorAll('button')) b.addEventListener('click', () => { for (const x of $(id).querySelectorAll('button')) x.classList.toggle('on', x === b); });
  };
  $('btn-auto').onclick = () => {
    choices('auto-spins', [10, 25, 50, 100], String, 25);
    choices('auto-loss', [10, 25, 50, 100].map((k) => k * stake()), rand, 25 * stake());
    open('sheet-auto');
  };
  $('auto-start').onclick = () => {
    const pick = (id: string) => Number(($(id).querySelector('button.on') as HTMLElement).dataset.v);
    auto = { left: pick('auto-spins'), loss: pick('auto-loss'), start: balance ?? 0 };
    close('sheet-auto');
    void loop();
  };

  const startedAt = Date.now();
  setInterval(() => { const s = Math.floor((Date.now() - startedAt) / 1000); $('clock').textContent = `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`; }, 1000);

  const land = async (stops: number[], strips: string[][]) => {
    await scene.reels.stop(stops, strips, sp(), (reel, tease) => {
      sound.stop(reel);
      const col = [0, 1, 2].map((row) => strips[reel][(stops[reel] + row) % strips[reel].length]);
      col.forEach((s, row) => { if (s === 'Pearl' || s === 'Coin') scene.scatterLanded(reel, row); });
      scene.tease(tease ? reel + 1 : null);
    });
  };

  /** The win so far, counted onto the balance the round started from (the stake already off). */
  let shownWin = 0, before = 0;
  const addWin = async (amount: number) => {
    if (amount <= 0) return;
    const from = shownWin, to = shownWin + amount;
    shownWin = to;
    await scene.tweens.to(from, to, sp().countUp(amount), (v) => { setWin(Math.round(v)); $('balance').textContent = rand(before + Math.round(v)); });
  };

  const playWays = async (v: PlayView<WaysDetail>, st: number) => {
    const [first, ...free] = v.detail.spins;
    await land(first.stops, baseStrips);
    await presentWays(first, st);
    if (free.length) {
      sound.feature();
      scene.coins(50);
      await overlay(`${free.length} FREE SPINS`, '', 'Wilds can be ×2 or ×3, and multiply every way through them', 'START', auto ? 2500 : undefined);
      scene.setFeatureMood(true);
      scene.reels.setStrips(freeStrips, randomStops(freeStrips));
      banner(true);
      let won = 0;
      for (const [i, spin] of free.entries()) {
        bannerText(`FREE SPINS <b>${free.length - i}</b> left · won <b>${rand(won)}</b>`);
        sound.spin();
        scene.clearWins();
        scene.reels.spin(sp());
        await sleep(sp().minSpin);
        await land(spin.stops, freeStrips);
        await presentWays(spin, st);
        won += money(spin.paid, st);
      }
      bannerText(`FREE SPINS won <b>${rand(won)}</b>`);
      await overlay('FREE SPINS WON', rand(won), '', 'COLLECT', auto ? 2500 : undefined);
      banner(false);
      scene.setFeatureMood(false);
      scene.reels.setStrips(baseStrips, randomStops(baseStrips));
    }
  };

  const presentWays = async (spin: WaysSpin, st: number) => {
    const cells: Cell[] = spin.wins.flatMap((w) => w.cells);
    if (spin.scatterUnits > 0) for (let r = 0; r < 5; r++) for (let row = 0; row < 3; row++) {
      const strips = spin.free ? freeStrips : baseStrips;
      if (strips[r][(spin.stops[r] + row) % strips[r].length] === 'Pearl') cells.push([r, row]);
    }
    if (spin.paid > 0) {
      sound.win(spin.paid / unitsPerStake);
      scene.showAmount(rand(money(spin.paid, st)));
      await Promise.all([scene.showCells(cells, sp().pulse), addWin(money(spin.paid, st))]);
    }
  };

  const playHold = async (v: PlayView<HoldDetail>, st: number) => {
    const d = v.detail;
    await land(d.stops, baseStrips);
    const label = (c: Coin) => ({ reel: c.cell[0], row: c.cell[1], text: JACKPOTS[c.stakes] ?? rand(c.stakes * st).replace(',00', '').replace('.00', ''), colour: JACKPOTS[c.stakes] ? 0xff8af0 : 0xffe08a });
    const coins: Coin[] = [...d.coins];
    scene.setLabels(coins.map(label));
    // The cap pays lines first; whatever the round paid beyond them is the hold's (d.featureUnits is already capped).
    const lineUnits = Math.min(d.lines.reduce((a, l) => a + l.units, 0), d.units);
    if (lineUnits > 0) {
      sound.win(lineUnits / unitsPerStake);
      scene.showAmount(rand(money(lineUnits, st)));
      await Promise.all([scene.showCells(d.lines.flatMap((l) => l.cells), sp().pulse), addWin(money(lineUnits, st))]);
    }
    // Coins that start nothing keep their values up until the next spin starts.
    if (!d.respins.length) return;
    sound.feature();
    scene.coins(60);
    await overlay('HOLD & WIN', '', `${coins.length} coins hold · 3 respins · every new coin resets them`, 'START', auto ? 2500 : undefined);
    scene.clearWins();
    scene.setFeatureMood(true);
    scene.setLocks(coins.map((c) => ({ reel: c.cell[0], row: c.cell[1], symbol: 'Coin' })));
    scene.reels.setStrips(freeStrips, [0, 0, 0, 0, 0]);
    scene.setLabels(coins.map(label));
    banner(true);
    for (const r of d.respins) {
      bannerText(`RESPINS <b>3</b> · coins <b>${coins.length}</b>`);
      sound.spin();
      scene.reels.spin(sp());
      await sleep(Math.max(250, sp().minSpin));
      await land([0, 0, 0, 0, 0], freeStrips);
      if (r.added.length) {
        coins.push(...r.added);
        scene.setLocks(coins.map((c) => ({ reel: c.cell[0], row: c.cell[1], symbol: 'Coin' })));
        scene.setLabels(coins.map(label));
        sound.scatter(Math.min(coins.length, 6));
      }
      bannerText(`RESPINS <b>${r.left}</b> · coins <b>${coins.length}</b>`);
      await sleep(r.added.length ? 450 : 250);
    }
    if (d.grand) {
      sound.big();
      scene.coins(200);
      await overlay('GRAND', rand(1000 * st), 'A full board', 'COLLECT', auto ? 3000 : undefined);
    }
    const feature = money(d.featureUnits, st);
    await overlay('HOLD & WIN', rand(feature), `${coins.length} coins`, 'COLLECT', auto ? 2500 : undefined);
    await addWin(feature);
    banner(false);
    scene.setFeatureMood(false);
    scene.setLocks([]);
    scene.setLabels([]);
    scene.reels.setStrips(baseStrips, randomStops(baseStrips));
  };

  const banner = (on: boolean) => {
    const b = $('feature');
    b.style.display = on ? 'flex' : 'none';
    b.style.top = `${Math.round(scene.layout().boardBottom + 10)}px`;
  };
  const bannerText = (html: string) => { $('feature').innerHTML = `<span>${html}</span>`; };

  const spinOnce = async (): Promise<boolean> => {
    busy = true;
    const st = stake();
    const start = balance;
    if (balance !== null) setBalance(balance - st);
    shownWin = 0;
    setWin(0);
    sound.spin();
    scene.clearWins();
    scene.setLabels([]);
    scene.reels.spin(sp());
    refreshControls();
    const since = performance.now();
    const k = key();
    let v: PlayView<WaysDetail | HoldDetail>;
    try {
      try {
        v = await call(`instant/${cfg.gameId}/play`, { stake: st, choices: {}, key: k });
      } catch (e) {
        // A dropped reply, not a lost round: the same key returns the round the server already played.
        if (!(e instanceof ApiError) || e.code !== 'network') throw e;
        await sleep(1200);
        v = await call(`instant/${cfg.gameId}/play`, { stake: st, choices: {}, key: k });
      }
    } catch (e) {
      await land(randomStops(baseStrips), baseStrips);
      scene.clearWins();
      setBalance(start);
      toast(describe(e));
      busy = false;
      refreshControls();
      return false;
    }
    before = v.balance === null ? (start ?? 0) - st : v.balance - v.win;
    $('balance').textContent = rand(before);
    const left = sp().minSpin - (performance.now() - since);
    if (left > 0) await sleep(left);
    if (cfg.mechanic === 'ways') await playWays(v as PlayView<WaysDetail>, st);
    else await playHold(v as PlayView<HoldDetail>, st);
    setWin(v.win);
    setBalance(v.balance);
    await bigWin(v.win, st);
    if (v.detail.capped) toast(`Maximum win reached: ${state.info.maxWinStakes.toLocaleString('en-ZA')}× stake`);
    state.recent = [{ roundId: v.roundId, stake: st, paid: v.win, status: 'settled', at: new Date().toISOString() }, ...state.recent].slice(0, 50);
    void refresh();
    busy = false;
    refreshControls();
    return true;
  };

  const loop = async () => {
    refreshControls();
    while (auto && auto.left > 0) {
      if (balance !== null && auto.start - balance + stake() > auto.loss) { toast('Autoplay stopped: loss limit reached.'); break; }
      const ok = await spinOnce();
      if (!ok || !auto) break;
      auto.left--;
      refreshControls();
      await sleep(sp().between);
    }
    auto = null;
    refreshControls();
  };

  $('spin').onclick = () => {
    if (auto) { auto = null; refreshControls(); return; }
    if (!busy) void spinOnce();
  };
  document.addEventListener('keydown', (e) => { if (e.code === 'Space' && !busy && !auto) { e.preventDefault(); void spinOnce(); } });

  setBalance(balance);
  renderHistory(state.recent);
  rules();
  refreshControls();
}
