import { Application, Assets, Container, FillGradient, Graphics, Sprite, Texture, type Ticker } from 'pixi.js';
import { assetUrl, rand, sound, Tweens, ease } from '@originals/slot-kit';
import { call, describe, key, minesLayout, mountShell, query, seedsHtml, times, type RoundSummary, type Seeds } from '@originals/instant-kit';

interface MinesView {
  roundId: string; stake: number; mines: number; revealed: number[]; multiplierBp: number; nextBp: number; win: number;
  status: 'open' | 'busted' | 'cashed' | string; mineTiles: number[] | null; nonce: number; clientSeed: string; serverSeedHash: string; balance: number | null;
}
interface MinesInfo { gameId: string; name: string; rtp: number; maxWinStakes: number; stakes: number[]; tiles: number; table: number[][] }
interface MinesState { balance: number | null; open: MinesView | null; recent: RoundSummary[]; seeds: Seeds; info: MinesInfo }

const MINE_CHOICES = [1, 3, 5, 10, 24];
const COLOURS = { tileTop: 0x2a4a6b, tileBottom: 0x16283f, tileEdge: 0x4f7aa6, hover: 0x5fd7ff, gem: 0x36e2a0, mine: 0xff4d5e, bg1: 0x0c2235, bg2: 0x050b14 };

let state: MinesState;
let round: MinesView | null = null;
let mines = 3;
let busy = false;

const boot = async () => {
  state = await call<MinesState>(query('instant/mines/state'));
  const shell = mountShell({ title: 'Mines', accent: '#36e2a0', accent2: '#12a874', background: '#050b14', stakes: state.info.stakes, rtp: state.info.rtp });
  shell.setBalance(state.balance);

  // The game's own controls: how many mines, and a random pick.
  shell.controls.innerHTML = `
    <div class="label">Mines</div>
    <div class="row"><div class="chips" id="mine-chips">${MINE_CHOICES.map((m) => `<button class="chip" data-m="${m}">${m}</button>`).join('')}</div>
    <div class="field" style="width:118px"><button id="m-down" aria-label="Fewer mines">−</button><input id="m-val" inputmode="numeric" value="3" aria-label="Mines"><button id="m-up" aria-label="More mines">+</button></div></div>`;
  const setMines = (m: number) => {
    mines = Math.min(24, Math.max(1, Math.round(m) || 1));
    (document.getElementById('m-val') as HTMLInputElement).value = String(mines);
    for (const c of document.querySelectorAll<HTMLElement>('#mine-chips .chip')) c.classList.toggle('on', Number(c.dataset.m) === mines);
    ladder();
    shell.rules(rules());
  };
  for (const c of document.querySelectorAll<HTMLElement>('#mine-chips .chip')) c.onclick = () => setMines(Number(c.dataset.m));
  document.getElementById('m-down')!.onclick = () => setMines(mines - 1);
  document.getElementById('m-up')!.onclick = () => setMines(mines + 1);
  (document.getElementById('m-val') as HTMLInputElement).onchange = (e) => setMines(Number((e.target as HTMLInputElement).value));

  // The multiplier ladder over the board: the next picks' multipliers, the current one lit.
  shell.stage.insertAdjacentHTML('beforeend', `<div id="ladder" style="position:absolute;left:0;right:0;top:40px;display:flex;gap:6px;justify-content:center;z-index:2;padding:0 10px;overflow:hidden"></div>`);
  const ladder = () => {
    const row = state.info.table[mines - 1];
    const picked = round?.status === 'open' ? round.revealed.length : 0;
    const from = Math.max(0, picked - 1);
    document.getElementById('ladder')!.innerHTML = row.slice(from, from + 5).map((bp, i) => {
      const n = from + i + 1, on = n === picked;
      return `<div style="flex:0 0 auto;padding:6px 10px;border-radius:10px;font-weight:800;font-size:13px;font-variant-numeric:tabular-nums;border:1px solid ${on ? 'var(--accent)' : 'rgba(255,255,255,.1)'};background:${on ? 'color-mix(in srgb, var(--accent) 22%, transparent)' : 'rgba(0,0,0,.35)'};color:${on ? 'var(--accent)' : '#cfe0f5'}"><small style="opacity:.6;font-weight:600;margin-right:4px">${n}</small>${times(bp)}</div>`;
    }).join('');
  };

  const rules = () => `
    <h2>Mines</h2>
    <p>25 tiles hide gems and mines. Choose how many mines (1–24) and your stake, then reveal tiles one at a time. Every gem raises the multiplier; cash out whenever you like. Reveal a mine and the round is lost.</p>
    <div class="facts"><div><b>${state.info.rtp.toFixed(2)}%</b><small>Return to player</small></div><div><b>${state.info.maxWinStakes.toLocaleString('en-ZA')}×</b><small>Max win</small></div><div><b>1–24</b><small>Mines</small></div></div>
    <h3>Multipliers with ${mines} mine${mines > 1 ? 's' : ''}</h3>
    <p class="note">After k gems the multiplier is 0.96 × C(25, k) ÷ C(25 − mines, k): the chance of surviving k picks, inverted, times the 96% return. Every way of playing returns 96%.</p>
    <table class="table"><tr><th>Gems</th><th>Multiplier</th><th>Pays at ${rand(shell.stake)}</th></tr>${state.info.table[mines - 1].map((bp, i) =>
      `<tr><td>${i + 1}</td><td>${times(bp)}</td><td>${rand(Math.floor(shell.stake * bp / 10000))}</td></tr>`).join('')}</table>
    <p>The round cashes out by itself when every gem is revealed, or before a pick would pass the ${state.info.maxWinStakes.toLocaleString('en-ZA')}× maximum.</p>
    <h3>Fairness</h3>
    <p>The mines are placed before you pick, from your seeds (see Provably fair), and stored before your stake is taken. An unfinished round is kept: reopen the game to carry on. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`;

  const fairness = () => {
    shell.fairness(seedsHtml(state.seeds) + (state.seeds.previousServerSeed ? `<p class="note">Verify needs the round's mines: <input id="verify-mines" type="number" min="1" max="24" value="${mines}" style="width:64px"></p>` : ''));
    const rotate = document.getElementById('rotate');
    if (rotate) rotate.onclick = async () => {
      try {
        state.seeds = await call<Seeds>('instant/seed', { clientSeed: (document.getElementById('client-seed') as HTMLInputElement).value.trim() });
        fairness();
      } catch (e) { shell.toast(describe(e)); }
    };
    const verify = document.getElementById('verify');
    if (verify && state.seeds.previousServerSeed) verify.onclick = async () => {
      const nonce = Number((document.getElementById('verify-nonce') as HTMLInputElement).value);
      const m = Number((document.getElementById('verify-mines') as HTMLInputElement).value);
      const tiles = await minesLayout(state.seeds.previousServerSeed!, state.seeds.previousClientSeed!, nonce, m);
      document.getElementById('verify-out')!.textContent = `Round ${nonce} with ${m} mines: mines on tiles ${tiles.map((t) => t + 1).join(', ')} (numbered 1–25, left to right, top to bottom).`;
    };
  };
  fairness();
  shell.history(state.recent);

  // ---- The board ----
  const app = new Application();
  await app.init({ resizeTo: shell.stage, background: COLOURS.bg2, antialias: true, resolution: Math.min(3, window.devicePixelRatio || 1), autoDensity: true });
  shell.stage.prepend(app.canvas);
  new ResizeObserver(() => app.resize()).observe(shell.stage);
  const tweens = new Tweens(app.ticker);
  const A = (f: string) => assetUrl(`assets/mines/${f}`);
  const tex = await Assets.load<Texture>([A('tile.webp'), A('diamond.webp'), A('mine.webp'), A('bg-landscape.jpg'), A('bg-portrait.jpg')]);
  const T = (f: string) => tex[A(f)] as Texture;
  for (const t of Object.values(tex)) { (t as Texture).source.autoGenerateMipmaps = true; (t as Texture).source.updateMipmaps(); }
  const backdrop = new Sprite(T('bg-landscape.jpg'));
  backdrop.anchor.set(0.5);
  const bg = new Graphics();
  const board = new Container();
  const fx = new Container();
  app.stage.addChild(backdrop, bg, board, fx);

  const tiles: Container[] = [];
  let tileSize = 80;

  const drawTile = (t: Container, face: 'hidden' | 'gem' | 'mine', dim = false) => {
    t.removeChildren().forEach((c) => c.destroy());
    const s = tileSize;
    const stone = new Sprite(T('tile.webp'));
    stone.anchor.set(0.5);
    stone.width = stone.height = s;
    t.addChild(stone);
    if (face === 'hidden') return;
    // Revealed: the stone darkens and glows in the find's colour, and the find sits on it.
    stone.tint = face === 'gem' ? 0x2f6f66 : 0x6a2a32;
    const halo = new Graphics().circle(0, 0, s * 0.42).fill({ color: face === 'gem' ? COLOURS.gem : COLOURS.mine, alpha: 0.22 });
    halo.blendMode = 'add';
    const find = new Sprite(T(face === 'gem' ? 'diamond.webp' : 'mine.webp'));
    find.anchor.set(0.5);
    find.width = find.height = s * (face === 'gem' ? 0.8 : 0.84);
    t.addChild(halo, find);
    if (dim) t.alpha = 0.5;
    else t.alpha = 1;
  };

  // Particles: gem sparkle and the mine's blast.
  interface P { g: Graphics; vx: number; vy: number; life: number; max: number; grav: number }
  const parts: P[] = [];
  const burst = (x: number, y: number, colour: number, n: number, speed: number, grav = 0.18) => {
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2, v = speed * (0.4 + Math.random() * 0.8);
      const g = new Graphics().circle(0, 0, 1.5 + Math.random() * 3).fill(colour);
      g.blendMode = 'add';
      g.position.set(x, y);
      fx.addChild(g);
      parts.push({ g, vx: Math.cos(a) * v, vy: Math.sin(a) * v - speed * 0.3, life: 0, max: 40 + Math.random() * 30, grav });
    }
  };
  app.ticker.add((t: Ticker) => {
    for (const p of [...parts]) {
      p.life += t.deltaTime;
      p.vy += p.grav * t.deltaTime;
      p.g.x += p.vx * t.deltaTime;
      p.g.y += p.vy * t.deltaTime;
      p.g.alpha = Math.max(0, 1 - p.life / p.max);
      if (p.life >= p.max) { p.g.destroy(); parts.splice(parts.indexOf(p), 1); }
    }
  });

  const shake = async () => {
    for (let i = 0; i < 8; i++) {
      board.x += (Math.random() - 0.5) * tileSize * 0.18;
      board.y += (Math.random() - 0.5) * tileSize * 0.18;
      await tweens.wait(28);
      layout(false);
    }
  };

  /** Fills the stage: the biggest square board that fits under the ladder. */
  const layout = (redraw = true) => {
    const w = app.screen.width, h = app.screen.height;
    const fill = new FillGradient({ type: 'radial', center: { x: 0.5, y: 0.45 }, innerRadius: 0, outerCenter: { x: 0.5, y: 0.45 }, outerRadius: 0.75,
      colorStops: [{ offset: 0, color: 0x000000 }, { offset: 1, color: COLOURS.bg2 }], textureSpace: 'local' });
    backdrop.texture = T(h > w ? 'bg-portrait.jpg' : 'bg-landscape.jpg');
    backdrop.scale.set(Math.max(w / backdrop.texture.width, h / backdrop.texture.height));
    backdrop.position.set(w / 2, h / 2);
    bg.clear().rect(0, 0, w, h).fill(fill);
    bg.alpha = 0.55;
    const top = 84, side = Math.min(w - 16, h - top - 10);
    const gap = side * 0.022;
    tileSize = (side - gap * 4) / 5;
    board.position.set((w - side) / 2 + tileSize / 2, top + (h - top - 10 - side) / 2 + tileSize / 2);
    if (!redraw) return;
    tiles.forEach((t, i) => {
      t.position.set((i % 5) * (tileSize + gap), Math.floor(i / 5) * (tileSize + gap));
      drawTile(t, faceOf(i), dimmed(i));
      t.hitArea = { contains: (x: number, y: number) => Math.abs(x) <= tileSize / 2 && Math.abs(y) <= tileSize / 2 };
    });
  };

  const faceOf = (i: number): 'hidden' | 'gem' | 'mine' => {
    if (!round) return 'hidden';
    if (round.mineTiles?.includes(i)) return round.revealed.includes(i) || round.status !== 'open' ? 'mine' : 'hidden';
    return round.revealed.includes(i) || (round.status !== 'open' && round.mineTiles) ? 'gem' : 'hidden';
  };
  const dimmed = (i: number) => !!round && round.status !== 'open' && !round.revealed.includes(i);

  for (let i = 0; i < 25; i++) {
    const t = new Container();
    t.eventMode = 'static';
    t.cursor = 'pointer';
    t.on('pointerover', () => { if (canPick(i)) t.scale.set(1.04); });
    t.on('pointerout', () => t.scale.set(1));
    t.on('pointertap', () => void pick(i));
    board.addChild(t);
    tiles.push(t);
  }
  const canPick = (i: number) => !busy && round?.status === 'open' && !round.revealed.includes(i);

  const flip = async (i: number, face: 'gem' | 'mine') => {
    const t = tiles[i];
    await tweens.to(1, 0, 90, (v) => t.scale.set(v, 1));
    drawTile(t, face);
    await tweens.to(0, 1, 260, (v) => t.scale.set(v, Math.max(0.6, v)), ease.outBack);
  };

  const world = (i: number) => ({ x: board.x + tiles[i].x, y: board.y + tiles[i].y });

  // ---- Play ----
  const show = () => {
    const open = round?.status === 'open';
    const a = shell.action;
    if (open && round!.revealed.length > 0) {
      a.className = 'action cash';
      a.innerHTML = `CASH OUT<small>${rand(Math.floor(round!.stake * round!.multiplierBp / 10000))} · ${times(round!.multiplierBp)}</small>`;
      a.disabled = busy;
    } else if (open) {
      a.className = 'action';
      a.innerHTML = 'PICK A TILE';
      a.disabled = true;
    } else {
      a.className = 'action';
      a.innerHTML = 'BET';
      a.disabled = busy;
    }
    shell.lock(!!open || busy);
    ladder();
  };

  const finish = async (view: MinesView) => {
    // The round is over: show every tile, then refresh history and seeds.
    round = view;
    shell.setBalance(view.balance);
    layout();
    shell.setWin(view.win);
    if (view.status === 'cashed') {
      sound.win(view.multiplierBp >= 50_000 ? 3 : 1);
      if (view.multiplierBp >= 100_000) shell.bigWin(view.win, view.multiplierBp);
    }
    try {
      const s = await call<MinesState>(query('instant/mines/state'));
      state.seeds = s.seeds;
      shell.history(s.recent);
      fairness();
    } catch { /* history refreshes next round */ }
  };

  const pick = async (i: number) => {
    if (!canPick(i)) return;
    busy = true;
    show();
    try {
      const view = await call<MinesView>('instant/mines/pick', { roundId: round!.roundId, tile: i });
      const hitMine = view.status === 'busted';
      await flip(i, hitMine ? 'mine' : 'gem');
      const p = world(i);
      if (hitMine) {
        burst(p.x, p.y, COLOURS.mine, 70, tileSize * 0.16, 0.12);
        burst(p.x, p.y, 0xffc36b, 40, tileSize * 0.1, 0.08);
        sound.stop(0);
        void shake();
        await tweens.wait(420);
      } else {
        burst(p.x, p.y, COLOURS.gem, 18, tileSize * 0.06, 0.05);
        sound.scatter(Math.min(view.revealed.length, 6));
      }
      if (view.status === 'open') {
        round = view;
      } else {
        await finish(view);
      }
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      busy = false;
      show();
    }
  };

  shell.action.onclick = async () => {
    if (busy) return;
    if (round?.status === 'open') {
      busy = true;
      show();
      try {
        await finish(await call<MinesView>('instant/mines/cashout', { roundId: round.roundId }));
      } catch (e) {
        shell.toast(describe(e));
      } finally {
        busy = false;
        show();
      }
      return;
    }
    busy = true;
    show();
    try {
      const view = await call<MinesView>('instant/mines/start', { stake: shell.stake, mines, key: key() });
      round = view;
      shell.setBalance(view.balance);
      shell.setWin(0);
      layout();
      for (const [i, t] of tiles.entries()) void tweens.to(0.6, 1, 220 + i * 8, (v) => t.scale.set(v), ease.outBack);
      sound.spin();
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      busy = false;
      show();
    }
  };

  window.addEventListener('resize', () => layout());
  app.renderer.on('resize', () => layout());
  if (state.open) {
    round = state.open;
    mines = state.open.mines;
  }
  setMines(mines);
  layout();
  show();
};

boot().catch((e) => {
  document.body.innerHTML = `<p style="color:#eee;font-family:system-ui;padding:24px">${describe(e)}</p>`;
});
