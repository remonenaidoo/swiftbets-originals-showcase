import { Application, Assets, Container, FillGradient, Graphics, Sprite, Text, Texture, type Ticker } from 'pixi.js';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { assetUrl, rand, sound } from '@originals/slot-kit';
import { call, crashPoint, describe, key, mountShell, query, sha256, times, type RoundSummary, type Shell } from '@originals/instant-kit';

interface CrashPublic { id: string; seq: number; seedHash: string; phase: 'betting' | 'flying' | 'crashed' | 'void'; bettingUntil: string; serverNow: string; rate: number; pointBp: number | null; seed: string | null }
interface CrashBetView { roundId: string; table: string; stake: number; autoBp: number | null; status: 'open' | 'cashed' | 'lost' | 'refunded'; cashedAtBp: number | null; win: number; balance: number | null }
interface CrashInfo { gameId: string; name: string; rtp: number; maxWinStakes: number; stakes: number[]; minAutoBp: number; rate: number; bettingSeconds: number }
interface BetPublic { table: string; tag: string; player: string; stake: number; cashedBp: number | null; win: number | null }
interface CrashState { balance: number | null; round: CrashPublic | null; bets: CrashBetView[]; board: BetPublic[]; history: number[]; recent: RoundSummary[]; info: CrashInfo }

const QUICK = [500, 1000, 2000, 10000];

let state: CrashState;
let round: CrashPublic | null = null;
let offset = 0; // server clock − this clock, ms
let board: BetPublic[] = [];
const verified: Record<string, boolean> = {};

const now = () => Date.now() + offset;
const sync = (r: CrashPublic) => { offset = Date.parse(r.serverNow) - Date.now(); };
const elapsed = () => (round ? now() - Date.parse(round.bettingUntil) : 0);
const multAt = (ms: number) => (ms <= 0 ? 10000 : Math.floor(Math.exp(state.info.rate * ms) * 100) * 100);

/**
 * One bet box: its stake (the server's ladder, with quick picks), its auto cash-out and auto bet, and its button,
 * which is BET in the countdown, queues for the next round otherwise, and is CASH OUT while its bet flies.
 */
class Panel {
  bet: CrashBetView | null = null;
  queued = false;
  busy = false;
  private stakeIndex: number;
  private autoBp = 20000;
  private autoCash = false;
  private autoBet = false;
  readonly el: HTMLElement;
  private q = <T extends HTMLElement>(sel: string) => this.el.querySelector(sel) as T;

  constructor(readonly index: number, private readonly shell: Shell, private readonly onChange: () => void) {
    const stakes = state.info.stakes;
    const saved = (() => { try { return Number(localStorage.getItem(`crash-stake-${index}`)); } catch { return 0; } })();
    this.stakeIndex = Math.max(0, stakes.indexOf(saved || 200)) || Math.max(0, stakes.indexOf(200));
    this.el = document.createElement('div');
    this.el.className = 'betbox';
    this.el.innerHTML = `
      <div class="top">
        <div class="left">
          <div class="field"><button data-a="down" aria-label="Lower stake">−</button><input data-a="stake" readonly aria-label="Stake"><button data-a="up" aria-label="Raise stake">+</button></div>
          <div class="quick">${QUICK.map((s) => `<button data-q="${s}">${rand(s).replace(',00', '').replace('.00', '')}</button>`).join('')}</div>
        </div>
        <button class="go" data-a="go"></button>
      </div>
      <div class="opts">
        <button class="toggle" data-a="autobet">Auto bet <i></i></button>
        <button class="toggle" data-a="autocash">Auto cash-out <i></i></button>
        <div class="field target"><input data-a="target" inputmode="decimal" value="2.00" aria-label="Auto cash-out multiplier"></div>
      </div>`;
    this.q('[data-a=down]').onclick = () => this.setStake(this.stakeIndex - 1);
    this.q('[data-a=up]').onclick = () => this.setStake(this.stakeIndex + 1);
    for (const b of this.el.querySelectorAll<HTMLElement>('[data-q]')) b.onclick = () => this.setStake(stakes.indexOf(Number(b.dataset.q)));
    const target = this.q<HTMLInputElement>('[data-a=target]');
    target.onchange = () => {
      const v = Math.round(Number(target.value.replace(',', '.')) * 100) * 100;
      this.autoBp = Math.min(state.info.maxWinStakes * 10000, Math.max(state.info.minAutoBp, v || 20000));
      target.value = (this.autoBp / 10000).toFixed(2);
    };
    this.q('[data-a=autocash]').onclick = () => { this.autoCash = !this.autoCash; this.render(); };
    this.q('[data-a=autobet]').onclick = () => {
      this.autoBet = !this.autoBet;
      if (this.autoBet && !this.bet && !this.queued) void this.press();
      this.render();
    };
    this.q('[data-a=go]').onclick = () => void this.press();
    this.setStake(this.stakeIndex);
  }

  get stake() { return state.info.stakes[this.stakeIndex]; }

  private setStake(i: number) {
    if (i < 0 || i >= state.info.stakes.length) return;
    this.stakeIndex = i;
    try { localStorage.setItem(`crash-stake-${this.index}`, String(this.stake)); } catch { /* private mode */ }
    this.render();
  }

  /** A new round's countdown: a queued (or auto) bet goes in now; a finished bet clears. */
  newRound() {
    // A finished bet clears; so does one from an earlier round (an interrupted one), which can no longer be cashed.
    if (this.bet && (this.bet.status !== 'open' || this.bet.table !== round?.id)) this.bet = null;
    if (!this.bet && (this.queued || this.autoBet)) { this.queued = false; void this.place(); }
    this.render();
  }

  async press() {
    if (this.busy) return;
    if (this.bet?.status === 'open' && round?.phase === 'flying') return this.cashOut();
    if (this.bet?.status === 'open') return;
    if (this.queued) { this.queued = false; this.autoBet = false; this.render(); return; }
    if (round?.phase === 'betting' && elapsed() < -300) return this.place();
    this.queued = true;
    this.render();
  }

  private async place() {
    this.busy = true;
    this.render();
    try {
      this.bet = await call<CrashBetView>('instant/crash/bet', { stake: this.stake, autoBp: this.autoCash ? this.autoBp : null, key: key() });
      this.shell.setBalance(this.bet.balance);
      sound.spin();
    } catch (e) {
      this.autoBet = false;
      this.shell.toast(describe(e));
    } finally {
      this.busy = false;
      this.render();
      this.onChange();
    }
  }

  private async cashOut() {
    this.busy = true;
    try {
      const cash = await call<CrashBetView>('instant/crash/cashout', { roundId: this.bet!.roundId });
      this.bet = cash;
      this.shell.setBalance(cash.balance);
      this.shell.setWin(cash.win);
      sound.win(cash.cashedAtBp! >= 50000 ? 3 : 1);
      if (cash.cashedAtBp! >= 100000) this.shell.bigWin(cash.win, cash.cashedAtBp!);
    } catch (e) {
      this.shell.toast(describe(e));
    } finally {
      this.busy = false;
      this.render();
      this.onChange();
    }
  }

  /** The cash-out amount, every frame while the bet flies (an auto target already passed shows as paid). */
  live(m: number) {
    if (this.bet?.status !== 'open' || this.busy) return;
    const go = this.q('[data-a=go]');
    if (this.bet.autoBp && m >= this.bet.autoBp) {
      go.className = 'go placed';
      go.innerHTML = `CASHED ${times(this.bet.autoBp)}<small>${rand(Math.floor(this.bet.stake * this.bet.autoBp / 10000))}</small>`;
      return;
    }
    go.className = 'go cash';
    go.innerHTML = `CASH OUT<small>${rand(Math.floor(this.bet.stake * m / 10000))}</small>`;
  }

  render() {
    const go = this.q<HTMLButtonElement>('[data-a=go]');
    const open = this.bet?.status === 'open';
    this.q<HTMLInputElement>('[data-a=stake]').value = rand(this.stake);
    this.q('[data-a=autobet]').classList.toggle('on', this.autoBet);
    this.q('[data-a=autocash]').classList.toggle('on', this.autoCash);
    const lock = open || this.queued || this.busy;
    for (const b of this.el.querySelectorAll<HTMLButtonElement | HTMLInputElement>('.left button, [data-a=target], [data-a=autocash]')) b.disabled = lock;
    go.disabled = this.busy;
    const crashed = round?.phase === 'crashed' || round?.phase === 'void';
    if (open && round?.phase === 'flying') {
      go.className = 'go cash';
    } else if (open && crashed && !(this.bet!.autoBp && round?.pointBp && this.bet!.autoBp <= round.pointBp)) {
      go.className = 'go placed';
      go.innerHTML = `LOST<small>${rand(this.bet!.stake)} · crashed</small>`;
    } else if (open && crashed) {
      go.className = 'go placed';
      go.innerHTML = `CASHED ${times(this.bet!.autoBp!)}<small>${rand(Math.floor(this.bet!.stake * this.bet!.autoBp! / 10000))}</small>`;
    } else if (open) {
      go.className = 'go placed';
      go.innerHTML = `BET PLACED<small>${rand(this.bet!.stake)} · waiting</small>`;
    } else if (this.queued) {
      go.className = 'go wait';
      go.innerHTML = 'CANCEL<small>waiting for next round</small>';
    } else if (this.bet?.status === 'cashed') {
      go.className = 'go placed';
      go.innerHTML = `WON ${rand(this.bet.win)}<small>${this.bet.cashedAtBp ? `at ${times(this.bet.cashedAtBp)}` : 'cashed out'}</small>`;
    } else if (this.bet?.status === 'refunded') {
      go.className = 'go placed';
      go.innerHTML = `REFUNDED<small>${rand(this.bet.stake)} · round interrupted</small>`;
    } else {
      go.className = 'go';
      go.innerHTML = `BET<small>${rand(this.stake)}</small>`;
    }
  }
}

let panels: Panel[] = [];

const boot = async () => {
  state = await call<CrashState>(query('instant/crash/state'));
  round = state.round;
  board = state.board;
  if (round) sync(round);
  const shell = mountShell({ title: 'Crash', accent: '#ff6fd8', accent2: '#b23ea0', background: '#03040c', stakes: state.info.stakes, rtp: state.info.rtp, layout: 'stacked', ownActions: true });
  shell.setBalance(state.balance);
  shell.history(state.recent);

  // Two bet boxes and the round's bets, in the panel.
  const drawBoard = () => {
    const el = document.getElementById('board');
    if (!el) return;
    const mine = new Set(panels.map((p) => p.bet?.roundId).filter(Boolean));
    el.innerHTML = `<div class="head"><span>Bets this round</span><span>${board.length} bet${board.length === 1 ? '' : 's'}</span></div>
      <table>${board.slice(0, 30).map((b) => `<tr class="${b.cashedBp ? 'won' : ''}"><td>${b.player}</td><td>${rand(b.stake)}</td><td>${b.cashedBp ? times(b.cashedBp) : ''}</td><td>${b.win ? rand(b.win) : ''}</td></tr>`).join('')}</table>
      ${board.length ? '' : '<p class="note" style="margin:6px 0">No bets yet. Be the first.</p>'}`;
    void mine;
  };
  const changed = () => drawBoard();
  panels = [new Panel(0, shell, changed), new Panel(1, shell, changed)];
  for (const p of panels) shell.controls.append(p.el);
  shell.controls.insertAdjacentHTML('beforeend', '<div class="bets" id="board"></div>');
  // A reload mid-round puts this player's bets back in their boxes.
  state.bets.forEach((b, i) => { if (panels[i]) { panels[i].bet = b; panels[i].render(); } });
  drawBoard();

  shell.rules(`
    <h2>Crash</h2>
    <p>One round for everyone. Bet while the countdown runs; then the rocket takes off and the multiplier climbs until it crashes. Cash out before the crash to win your stake times the multiplier at that moment. You can hold two bets in a round, each with its own stake and auto cash-out.</p>
    <div class="facts"><div><b>${state.info.rtp.toFixed(2)}%</b><small>Return to player</small></div><div><b>${state.info.maxWinStakes.toLocaleString('en-ZA')}×</b><small>Max multiplier</small></div><div><b>${state.info.bettingSeconds} s</b><small>To bet</small></div></div>
    <h3>How the crash point is set</h3>
    <p>Each round's crash point is fixed before betting opens: the chance it reaches at least x is 0.96 ÷ x, so every auto cash-out target returns 96%. About 1 round in 20 crashes at 1.00×, before anyone can cash out. The multiplier doubles about every 11.6 seconds.</p>
    <h3>Cashing out</h3>
    <p>A cash-out counts at the moment the server receives it, at the multiplier shown then (to 0.01×). If it arrives at or after the crash, the bet is lost. An auto cash-out is paid at exactly its target when the round reaches it. Auto bet places the same bet at the start of every round until you switch it off or a bet is refused.</p>
    <h3>Fairness</h3>
    <p>Before betting, every player sees the round's seed hash. After the crash the seed is shown: SHA-256 of the seed matches the hash, and the crash point is floor(96 ÷ (1 − u)) ÷ 100, with u the first HMAC-SHA256 float of (seed, "crash:round number:0"). The fairness panel shows the check for the last round.</p>
    <p>If the game stops mid-round, every open bet of that round is refunded. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);
  const fairness = () => shell.fairness(`
    <h2>Provably fair</h2>
    <p>Crash is one shared round, so it uses the round's own seed, not yours. Each round's seed hash is published before betting opens and its seed after it crashes. This page checks every crash it sees.</p>
    <h3>This round</h3>
    <div class="kv"><small>Round</small><code>#${round?.seq ?? '–'}</code></div>
    <div class="kv"><small>Seed hash</small><code>${round?.seedHash ?? '–'}</code></div>
    ${round?.seed ? `<div class="kv"><small>Seed</small><code>${round.seed}</code></div><div class="kv"><small>Crash point</small><code>${times(round.pointBp!)} ${verified[round.id] === true ? '✓ verified' : verified[round.id] === false ? '✗ does not match' : ''}</code></div>` : ''}`);
  fairness();

  // History strip over the stage.
  shell.stage.insertAdjacentHTML('beforeend', `
    <div id="hist" style="position:absolute;left:0;right:0;top:38px;display:flex;gap:6px;padding:0 10px;overflow:hidden;z-index:2;mask-image:linear-gradient(90deg,#000 85%,transparent)"></div>
    <div id="players" style="position:absolute;right:10px;bottom:30px;z-index:2;font-size:12px;font-weight:800;color:#cfd6ee;background:rgba(0,0,0,.45);border:1px solid rgba(255,255,255,.1);border-radius:999px;padding:4px 10px">👤 0</div>`);
  const pill = (bp: number) => {
    const c = bp >= 100000 ? '#ffd166' : bp >= 20000 ? '#36e2a0' : '#9aa6c4';
    return `<span style="flex:0 0 auto;padding:4px 9px;border-radius:999px;font-weight:800;font-size:12px;font-variant-numeric:tabular-nums;color:${c};background:rgba(0,0,0,.42);border:1px solid ${c}55">${times(bp)}</span>`;
  };
  const drawHistory = () => { document.getElementById('hist')!.innerHTML = state.history.map(pill).join(''); };
  const drawPlayers = () => { document.getElementById('players')!.textContent = `👤 ${new Set(board.map((b) => b.player)).size}`; };
  drawHistory();
  drawPlayers();

  // ---- The stage ----
  // A launch over Cape Town at night: the camera climbs with the multiplier, from the city through the clouds into
  // space. Layers are the rendered art; the curve, exhaust and explosion are drawn live.
  const A = (f: string) => assetUrl(`assets/crash/${f}`);
  const app = new Application();
  await app.init({ resizeTo: shell.stage, background: 0x05040f, antialias: true, resolution: Math.min(3, window.devicePixelRatio || 1), autoDensity: true });
  shell.stage.prepend(app.canvas);
  new ResizeObserver(() => app.resize()).observe(shell.stage);
  const tex = await Assets.load<Texture>([A('rocket.webp'), A('city.webp'), A('clouds.webp'), A('space-landscape.jpg'), A('space-portrait.jpg')]);
  const T = (f: string) => tex[A(f)] as Texture;
  for (const t of Object.values(tex)) { (t as Texture).source.autoGenerateMipmaps = true; (t as Texture).source.updateMipmaps(); }
  const ROCKET_NOSE = -0.525; // the render's nose direction (process.py)
  const NOZZLE = { x: 0.07, y: 0.86 }; // where the exhaust leaves, as a fraction of the sprite

  // A soft round particle, drawn once.
  const soft = (() => {
    const c = document.createElement('canvas');
    c.width = c.height = 64;
    const g = c.getContext('2d')!;
    const r = g.createRadialGradient(32, 32, 0, 32, 32, 32);
    r.addColorStop(0, 'rgba(255,255,255,1)');
    r.addColorStop(0.35, 'rgba(255,255,255,.55)');
    r.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = r;
    g.fillRect(0, 0, 64, 64);
    return Texture.from(c);
  })();

  const sky = new Graphics();
  const space = new Sprite(T('space-landscape.jpg'));
  const stars = new Container();
  const city = new Sprite(T('city.webp'));
  const cloudA = new Sprite(T('clouds.webp')), cloudB = new Sprite(T('clouds.webp'));
  const area = new Graphics(), glow = new Graphics(), line = new Graphics();
  const trail = new Container(), fx = new Container();
  const rocket = new Sprite(T('rocket.webp'));
  const flash = new Graphics();
  const textStyle = (top: number, bottom: number, shadow: number) => ({
    fontFamily: 'Trebuchet MS, system-ui, sans-serif', fontWeight: '900' as const, fontSize: 64,
    fill: new FillGradient({ type: 'linear', start: { x: 0, y: 0 }, end: { x: 0, y: 1 }, colorStops: [{ offset: 0, color: top }, { offset: 1, color: bottom }], textureSpace: 'local' }),
    stroke: { color: 0x0a0620, width: 4 }, dropShadow: { color: shadow, alpha: 0.45, blur: 12, distance: 0 }, padding: 32,
  });
  const big = new Text({ text: '', style: textStyle(0xffffff, 0xd6f4ff, 0x3aa8ff) });
  const sub = new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui, sans-serif', fontWeight: '800', fontSize: 18, fill: 0xd9def5, dropShadow: { color: 0x000000, alpha: 0.7, blur: 8, distance: 0 } } });
  big.anchor.set(0.5);
  sub.anchor.set(0.5);
  const bar = new Graphics();
  rocket.anchor.set(0.5);
  for (const c of [cloudA, cloudB]) c.anchor.set(0, 0);
  city.anchor.set(0.5, 1);
  space.anchor.set(0.5);
  glow.blendMode = 'add';
  trail.blendMode = 'add';
  app.stage.addChild(sky, space, stars, city, cloudA, cloudB, area, glow, line, trail, rocket, fx, big, sub, bar, flash);

  const starList: { g: Graphics; depth: number; tw: number }[] = [];
  for (let i = 0; i < 160; i++) {
    const depth = Math.random() < 0.7 ? 0.35 : 1;
    const g = new Graphics().circle(0, 0, depth > 0.5 ? 1.5 : 0.9).fill(0xffffff);
    g.position.set(Math.random() * 2400, Math.random() * 900);
    stars.addChild(g);
    starList.push({ g, depth, tw: Math.random() * 6 });
  }

  interface P { s: Sprite; vx: number; vy: number; life: number; max: number; grow: number; drag: number; grav: number }
  const parts: P[] = [];
  const emit = (layer: Container, x: number, y: number, colour: number, size: number, vx: number, vy: number, max: number, grow = 1.02, drag = 0.97, grav = 0) => {
    const s = new Sprite(soft);
    s.anchor.set(0.5);
    s.tint = colour;
    s.position.set(x, y);
    s.scale.set(size / 64);
    s.blendMode = 'add';
    layer.addChild(s);
    parts.push({ s, vx, vy, life: 0, max, grow, drag, grav });
  };

  let W = 0, H = 0, X0 = 0, Y0 = 0, PW = 0, PH = 0, portrait = false;
  const layout = () => {
    W = app.screen.width; H = app.screen.height; portrait = H > W;
    space.texture = T(portrait ? 'space-portrait.jpg' : 'space-landscape.jpg');
    space.scale.set(Math.max(W / space.texture.width, H / space.texture.height) * 1.08);
    space.position.set(W / 2, H / 2);
    city.scale.set(Math.max((W * 1.15) / city.texture.width, (H * 0.62) / city.texture.height));
    const cs = Math.max((W * 1.05) / cloudA.texture.width, (H * 0.9) / cloudA.texture.height);
    cloudA.scale.set(cs);
    cloudB.scale.set(cs);
    X0 = 44; Y0 = H - 30; PW = W - X0 - 30; PH = Y0 - 86;
    big.style.fontSize = Math.round(Math.min(W * 0.13, H * 0.15, 96));
    sub.style.fontSize = Math.round(Math.max(14, Math.min(W, H) * 0.04));
    big.position.set(W / 2, 70 + big.style.fontSize * 0.6);
    sub.position.set(W / 2, big.y + big.style.fontSize * 0.75);
    rocket.scale.set((Math.min(W, H) * (portrait ? 0.24 : 0.19)) / rocket.texture.width);
  };
  layout();
  app.renderer.on('resize', layout);

  const smooth = (a: number, b: number, x: number) => { const t = Math.min(1, Math.max(0, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
  const mix = (c1: number, c2: number, k: number) => {
    const r = (c1 >> 16) + (((c2 >> 16) - (c1 >> 16)) * k), g = ((c1 >> 8) & 255) + ((((c2 >> 8) & 255) - ((c1 >> 8) & 255)) * k), b = (c1 & 255) + (((c2 & 255) - (c1 & 255)) * k);
    return (Math.round(r) << 16) | (Math.round(g) << 8) | Math.round(b);
  };
  let crashShownFor = '';
  let shown = 0;
  let climb = 0; // how far the camera has risen this flight, in px
  let shake = 0;
  let drift = 0;
  app.ticker.add((tk: Ticker) => {
    const dt = tk.deltaTime;
    for (const p of [...parts]) {
      p.life += dt;
      p.vx *= p.drag; p.vy = p.vy * p.drag + p.grav * dt;
      p.s.x += p.vx * dt; p.s.y += p.vy * dt;
      p.s.scale.set(p.s.scale.x * Math.pow(p.grow, dt));
      p.s.alpha = Math.max(0, 1 - p.life / p.max);
      if (p.life >= p.max) { p.s.destroy(); parts.splice(parts.indexOf(p), 1); }
    }
    const phase = round?.phase;
    const ms = elapsed();
    const flying = phase === 'flying' && !round?.pointBp;
    const over = phase === 'crashed' || phase === 'void';
    const m = over ? (round?.pointBp ?? 10000) : flying ? multAt(ms) : 10000;

    // Rising: the world falls past the rocket. `climb` is how far the camera has risen (px); it speeds up with the
    // multiplier, and each layer moves at its own rate: the city drops away, cloud banks rush down past the rocket,
    // stars stream by. Colour stays quiet: navy to black, white stars, a faint nebula only very high up.
    // Speed in screen heights, so a phone and a desktop climb alike; the sky's colour follows the multiplier.
    const rate = H * (flying ? 0.005 + 0.01 * Math.log(m / 10000 + 1) : over ? 0.001 : 0);
    climb = phase === 'betting' ? climb + (0 - climb) * Math.min(1, 0.06 * dt) : climb + rate * dt;
    const p = phase === 'betting' ? 0 : Math.min(1, Math.log(m / 10000) / Math.log(50));
    sky.clear().rect(0, 0, W, H).fill(new FillGradient({ type: 'linear', start: { x: 0, y: 0 }, end: { x: 0, y: 1 },
      colorStops: [{ offset: 0, color: mix(0x0b1236, 0x010106, p) }, { offset: 1, color: mix(0x1c1a44, 0x03030b, p) }], textureSpace: 'local' }));
    space.alpha = 0.32 * smooth(0.45, 1, p);
    space.tint = 0x8f9fd0;
    space.y = H / 2 + climb * 0.02;
    city.position.set(W / 2, H + climb * 0.9);
    city.visible = city.y - city.height < H;
    // Cloud banks at fixed heights: each comes in from above and falls through the screen as the rocket climbs.
    const banks = [H * 1.1, H * 3.4, H * 6.2];
    const near = banks.map((alt) => H / 2 + (climb * 1.35 - alt)).filter((y) => y > -cloudA.height && y < H + cloudA.height);
    drift = (drift + dt * 0.3) % (cloudA.width || 1);
    for (const [i, c] of [cloudA, cloudB].entries()) {
      const y = near[i];
      c.visible = y !== undefined;
      if (y !== undefined) { c.position.set(-drift + (i ? -cloudA.width * 0.3 : 0), y - c.height / 2); c.tint = 0xa9b6e6; c.alpha = 0.75; }
    }
    stars.alpha = 0.35 + 0.65 * smooth(0.05, 0.4, p);
    const streak = 1 + Math.min(7, (rate / Math.max(1, H)) * 420);
    for (const s of starList) {
      s.tw += dt * 0.05;
      s.g.alpha = (0.55 + Math.sin(s.tw) * 0.35) * (s.depth > 0.5 ? 1 : 0.6);
      s.g.y += (0.08 + rate * 1.6 * s.depth) * dt;
      s.g.x -= rate * 1.0 * s.depth * dt;
      if (s.g.x < -8) s.g.x += W + 16;
      s.g.scale.set(1, streak * s.depth + (1 - s.depth));
      s.g.rotation = Math.atan2(1.0, 1.6); // along the stream: down and back, away from the climb
      // Back in above the top at a random height, so the stream never bunches into rows.
      if (s.g.y > H + 8) { s.g.y = -8 - Math.random() * H * 0.6; s.g.x = Math.random() * W; }
    }

    const tEnd = over ? Math.max(0, Math.log((round?.pointBp ?? 10000) / 10000) / state.info.rate) : Math.max(0, ms);

    area.clear(); glow.clear(); line.clear(); bar.clear(); flash.alpha = Math.max(0, flash.alpha - 0.04 * dt);
    const nozzle = () => {
      const w = rocket.width, h = rocket.height, lx = (NOZZLE.x - 0.5) * w, ly = (NOZZLE.y - 0.5) * h, c = Math.cos(rocket.rotation), s = Math.sin(rocket.rotation);
      return { x: rocket.x + lx * c - ly * s, y: rocket.y + lx * s + ly * c };
    };
    const exhaust = (power: number) => {
      const n = nozzle(), back = rocket.rotation + ROCKET_NOSE + Math.PI;
      for (let i = 0; i < 2 + power * 3; i++) {
        const a = back + (Math.random() - 0.5) * 0.35, v = (2 + Math.random() * 3) * (0.6 + power);
        const size = rocket.width * (0.08 + Math.random() * 0.1) * (0.7 + power * 0.6);
        emit(trail, n.x, n.y, Math.random() < 0.5 ? 0x5ad8ff : Math.random() < 0.5 ? 0xff5fd2 : 0xffffff, size, Math.cos(a) * v, Math.sin(a) * v, 16 + Math.random() * 18, 1.035, 0.95);
      }
    };

    if (phase === 'betting') {
      const left = Math.max(0, -ms);
      big.text = `${(left / 1000).toFixed(1)}s`;
      big.style = textStyle(0xffffff, 0xd6dcff, 0x6c5cff);
      sub.text = panels.some((x) => x.queued || x.bet?.status === 'open') ? 'Your bet is in. Get ready…' : 'Place your bets';
      const k = Math.min(1, left / (state.info.bettingSeconds * 1000));
      bar.roundRect(W / 2 - 120, sub.y + 22, 240, 6, 3).fill({ color: 0xffffff, alpha: 0.12 }).roundRect(W / 2 - 120, sub.y + 22, 240 * k, 6, 3).fill(0xff6fd8);
      rocket.visible = true;
      rocket.rotation = -Math.PI / 2 - ROCKET_NOSE; // standing on the pad, nose up
      rocket.position.set(X0 + rocket.width * 0.45, H - rocket.height * 0.55 - 6 + Math.sin(performance.now() / 300) * 1.5);
      if (Math.random() < 0.5) exhaust(0.05);
      shown = 0;
    } else if (round) {
      // The flight: the rocket lifts off and climbs to a cruising spot up and to the right in under two seconds, then
      // holds there, bobbing, while the world streams past (the climb above). The trail bends up behind it to the pad.
      const lift = smooth(0, 1800, tEnd), bob = lift >= 1 && !over ? Math.sin(performance.now() / 420) : 0;
      const start = { x: X0 + rocket.width * 0.45, y: Y0 };
      const cruise = { x: X0 + PW * (portrait ? 0.6 : 0.66), y: Y0 - PH * (portrait ? 0.55 : 0.6) };
      const end = { x: start.x + (cruise.x - start.x) * lift + bob * 3, y: start.y + (cruise.y - start.y) * lift + bob * 6 };
      const n = 60, k = 1.6, pts: { x: number; y: number }[] = [];
      for (let i = 0; i <= n; i++) {
        const u = i / n;
        pts.push({ x: start.x + (end.x - start.x) * u, y: start.y - (start.y - end.y) * ((Math.exp(k * u) - 1) / (Math.exp(k) - 1)) });
      }
      area.moveTo(pts[0].x, Y0);
      for (const q of pts) area.lineTo(q.x, q.y);
      area.lineTo(pts[pts.length - 1].x, Y0).closePath()
        .fill(new FillGradient({ type: 'linear', start: { x: 0, y: 0 }, end: { x: 0, y: 1 }, colorStops: [{ offset: 0, color: over ? 0xff4d5e : 0x9fe7ff }, { offset: 1, color: 0x0b1236 }], textureSpace: 'local' }));
      area.alpha = 0.06;
      glow.moveTo(pts[0].x, pts[0].y);
      line.moveTo(pts[0].x, pts[0].y);
      for (const q of pts) { glow.lineTo(q.x, q.y); line.lineTo(q.x, q.y); }
      glow.stroke({ width: 10, color: over ? 0xff4d5e : 0x9fe7ff, alpha: 0.14, cap: 'round', join: 'round' });
      line.stroke({ width: 3, color: over ? 0xff8090 : 0xe9f6ff, cap: 'round', join: 'round' });
      const tip = pts[pts.length - 1], prev = pts[Math.max(0, pts.length - 6)];
      if (!over) {
        rocket.visible = true;
        const heading = Math.atan2(tip.y - prev.y, tip.x - prev.x) || -0.2;
        rocket.rotation = heading - ROCKET_NOSE;
        rocket.position.set(tip.x, tip.y);
        exhaust(Math.min(1, 0.4 + p));
        big.text = times(m);
        big.style = m >= 100000 ? textStyle(0xfff6c9, 0xffc95a, 0xff9a2e) : textStyle(0xffffff, 0xd6f4ff, 0x3aa8ff);
        const live = panels.filter((x) => x.bet?.status === 'open').reduce((a, x) => a + x.bet!.stake, 0);
        sub.text = live ? `Your bets now: ${rand(Math.floor(live * m / 10000))}` : '';
        if (m >= shown + 10000 && m >= 20000) { shown = Math.floor(m / 10000) * 10000; sound.tick(); }
      } else {
        rocket.visible = false;
        big.text = times(round.pointBp!);
        big.style = textStyle(0xffd0d5, 0xff3348, 0xff0030);
        sub.text = 'CRASHED';
        if (crashShownFor !== round.id) {
          crashShownFor = round.id;
          // The blast: a flash, a shock ring, fire and sparks, a shake.
          flash.clear().rect(0, 0, W, H).fill(0xffe9f0);
          flash.alpha = 0.55;
          for (let i = 0; i < 70; i++) {
            const a = Math.random() * Math.PI * 2, v = 2 + Math.random() * 7;
            emit(fx, tip.x, tip.y, [0xff4d5e, 0xffb347, 0xff5fd2, 0xfff1a8][i % 4], rocket.width * (0.12 + Math.random() * 0.25), Math.cos(a) * v, Math.sin(a) * v, 30 + Math.random() * 40, 1.02, 0.94, 0.04);
          }
          for (let i = 0; i < 40; i++) {
            const a = Math.random() * Math.PI * 2, v = 6 + Math.random() * 9;
            emit(fx, tip.x, tip.y, 0xffffff, rocket.width * 0.05, Math.cos(a) * v, Math.sin(a) * v, 24 + Math.random() * 20, 0.99, 0.96, 0.12);
          }
          const ring = new Graphics().circle(0, 0, 10).stroke({ width: 4, color: 0xffd6e8 });
          ring.position.set(tip.x, tip.y);
          ring.blendMode = 'add';
          fx.addChild(ring);
          let k2 = 0;
          const grow = (t2: Ticker) => { k2 += t2.deltaTime; ring.scale.set(1 + k2 * 0.9); ring.alpha = Math.max(0, 1 - k2 / 34); if (k2 > 34) { app.ticker.remove(grow); ring.destroy(); } };
          app.ticker.add(grow);
          shake = 14;
          sound.stop(0);
        }
      }
    }
    if (shake > 0) { app.stage.position.set((Math.random() - 0.5) * shake, (Math.random() - 0.5) * shake); shake *= 0.86; if (shake < 0.4) { shake = 0; app.stage.position.set(0, 0); } }
    // Each cash-out button follows the multiplier while its bet is flying.
    if (flying) for (const x of panels) x.live(m);
  });

  // ---- Live round ----
  const renderAll = () => { for (const p of panels) p.render(); drawBoard(); drawPlayers(); };

  /** After a round, each bet's outcome and the balance come from the server. */
  const refresh = async () => {
    try {
      const s = await call<CrashState>(query('instant/crash/state'));
      shell.setBalance(s.balance);
      shell.history(s.recent);
      for (const p of panels) {
        if (p.bet?.status !== 'open') continue;
        const r = s.recent.find((x) => x.roundId === p.bet!.roundId);
        if (r) {
          // The server's verdict: refunded (the round was interrupted), won, or lost. The multiplier of a win learned
          // here is the auto target when there was one; a manual one is not in the summary, so only the amount shows.
          const status = r.status === 'voided' || r.status === 'refused' ? 'refunded' : r.paid > 0 ? 'cashed' : 'lost';
          p.bet = { ...p.bet, status, win: r.paid, cashedAtBp: status === 'cashed' ? p.bet.cashedAtBp ?? p.bet.autoBp : null };
          if (r.paid > 0) shell.setWin(r.paid);
        }
      }
      renderAll();
    } catch { /* next round refreshes */ }
  };

  const hub = new HubConnectionBuilder().withUrl(`${import.meta.env.BASE_URL}hubs/crash`).withAutomaticReconnect().configureLogging(LogLevel.Warning).build();
  hub.on('round', (r: CrashPublic) => {
    const fresh = r.id !== round?.id;
    round = r;
    sync(r);
    if (fresh) board = [];
    if (fresh && r.phase === 'betting') for (const p of panels) p.newRound();
    fairness();
    renderAll();
  });
  hub.on('tick', (r: CrashPublic) => { if (r.id === round?.id) { round = { ...round, ...r }; sync(r); } });
  hub.on('board', (b: BetPublic[]) => { board = b; drawBoard(); drawPlayers(); });
  hub.on('bet', (b: BetPublic) => { if (b.table === round?.id) { board = [b, ...board.filter((x) => x.tag !== b.tag)]; drawBoard(); drawPlayers(); } });
  hub.on('cashout', (b: BetPublic) => { if (b.table === round?.id) { board = board.map((x) => (x.tag === b.tag ? b : x)); if (!board.some((x) => x.tag === b.tag)) board.unshift(b); drawBoard(); } });
  hub.on('crashed', async (r: CrashPublic) => {
    round = r;
    sync(r);
    // Check the round: the seed's hash is the one shown before betting, and the seed gives this crash point.
    if (r.seed && r.pointBp) verified[r.id] = (await sha256(r.seed)) === r.seedHash && (await crashPoint(r.seed, r.seq)) === r.pointBp;
    fairness();
    renderAll();
    setTimeout(refresh, 1400);
  });
  hub.on('history', (h: number[]) => { state.history = h; drawHistory(); });
  hub.onreconnected(() => void refresh());
  await hub.start();
  renderAll();
};

boot().catch((e) => {
  document.body.innerHTML = `<p style="color:#eee;font-family:system-ui;padding:24px">${describe(e)}</p>`;
});
