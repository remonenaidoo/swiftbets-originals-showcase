import { Container, Graphics, Sprite, Text, type Ticker } from 'pixi.js';
import { rand, sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times, floats } from '@originals/instant-kit';

interface TowersView { roundId: string; stake: number; level: string; picked: number[]; multiplierBp: number; nextBp: number; win: number; status: string; traps: number[][] | null; balance: number | null }
interface TowersInfo { rows: number; levels: Record<string, { tiles: number; traps: number; table: number[] }> }

const LEVELS = ['easy', 'medium', 'hard', 'expert'];
let levels: TowersInfo['levels'] = {};

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant<TowersInfo, TowersView>({
    gameId: 'towers', title: 'Towers', accent: '#ffc24d', accent2: '#c7741f', background: '#0a0716', art: ['plank.webp', 'fruit.webp', 'snake.webp'],
    verify: async (s: string, c: string, n: number): Promise<string> => {
      const lv = (document.getElementById('verify-level') as HTMLSelectElement | null)?.value ?? 'easy';
      const { tiles, traps } = levels[lv];
      const f = await floats(s, c, n, 24);
      const rows = Array.from({ length: 8 }, (_, r) => {
        const row = Array.from({ length: tiles }, (_, i) => i);
        for (let i = tiles - 1; i > 0; i--) { const j = Math.floor(f[r * 3 + (tiles - 1 - i)] * (i + 1)); [row[i], row[j]] = [row[j], row[i]]; }
        return row.slice(0, traps).sort((a, b) => a - b).map((t) => t + 1).join('+');
      });
      return `on ${lv}, the traps were at ${rows.join(', ')} (rows 1–8, tiles numbered from the left).`;
    },
  });
  const info = state.info;
  levels = info.levels;
  let level = 'easy', busy = false;
  let round: TowersView | null = state.open;
  if (round) level = round.level;

  shell.controls.innerHTML = `<div class="label">Difficulty</div><div class="chips" id="levels">${LEVELS.map((l) => `<button class="chip${l === level ? ' on' : ''}" data-l="${l}">${l[0].toUpperCase() + l.slice(1)}</button>`).join('')}</div>`;
  for (const b of document.querySelectorAll<HTMLElement>('#levels .chip')) b.onclick = () => { level = b.dataset.l!; for (const x of document.querySelectorAll('#levels .chip')) x.classList.toggle('on', x === b); round = null; build(); rules(); show(); };

  const rules = () => shell.rules(`
    <h2>Towers</h2>
    <p>Climb the baobab one row at a time. Each row hides golden fruit and a cobra; pick a tile to climb. Every safe row raises the multiplier; cash out whenever you like. Find a cobra and the round is lost.</p>
    <div class="facts"><div><b>96.00%</b><small>Return to player</small></div><div><b>8</b><small>Rows</small></div><div><b>4</b><small>Levels</small></div></div>
    <h3>${level[0].toUpperCase() + level.slice(1)}: ${info.levels[level].tiles} tiles, ${info.levels[level].traps} cobra${info.levels[level].traps > 1 ? 's' : ''} a row</h3>
    <table class="table"><tr><th>Row</th><th>Multiplier</th><th>Pays at ${rand(shell.stake)}</th></tr>${info.levels[level].table.map((bp, i) => `<tr><td>${i + 1}</td><td>${times(bp)}</td><td>${rand(Math.floor(shell.stake * bp / 10000))}</td></tr>`).join('')}</table>
    <p>The multiplier after k rows is 0.96 × (tiles ÷ safe tiles)^k: every way of playing returns 96%. The round cashes out by itself at the top, or before a row would pass 5,000×.</p>
    <h3>Fairness</h3>
    <p>The cobras are placed before you climb, from your seeds. Verify a round's level: <select id="verify-level">${LEVELS.map((l) => `<option>${l}</option>`).join('')}</select>. An unfinished climb is kept: reopen the game to carry on. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);
  rules();

  // ---- Stage ----
  const tweens = new Tweens(app.ticker);
  const shade = new Graphics(), board = new Container(), labels = new Container(), fx = new Container();
  app.stage.addChild(shade, board, labels, fx);
  let tile = 60, gap = 8, X0 = 0, Y0 = 0;
  const cells: Container[][] = [];
  const tilesOf = () => info.levels[level].tiles;
  const rowOf = () => (round?.status === 'open' ? round.picked.length : -1);

  const face = (c: Container, kind: 'hidden' | 'fruit' | 'snake', live = false, dim = false) => {
    c.removeChildren().forEach((x) => x.destroy());
    const plank = new Sprite(tex('plank.webp'));
    plank.anchor.set(0.5);
    plank.width = tile * 1.6; plank.height = tile;
    if (live) plank.tint = 0xfff1c8;
    else if (kind === 'hidden') plank.tint = 0xb9a58c;
    c.addChild(plank);
    if (live) c.addChild(new Graphics().roundRect(-tile * 0.8, -tile / 2, tile * 1.6, tile, 10).stroke({ width: 3, color: 0xffc24d, alpha: 0.9 }));
    if (kind !== 'hidden') {
      const s = new Sprite(tex(kind === 'fruit' ? 'fruit.webp' : 'snake.webp'));
      s.anchor.set(0.5);
      s.width = s.height = tile * 0.92;
      c.addChild(s);
    }
    c.alpha = dim ? 0.45 : 1;
  };

  const build = () => {
    const W = app.screen.width, H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x05030c, alpha: 0.35 });
    const t = tilesOf(), rows = info.rows;
    tile = Math.min((W - 80) / (t * 1.6 + (t - 1) * 0.15), (H - 70) / (rows + (rows - 1) * 0.18));
    gap = tile * 0.16;
    const bw = t * tile * 1.6 + (t - 1) * gap;
    X0 = (W - bw) / 2 + 24 + tile * 0.8; Y0 = H - 14 - tile / 2;
    board.removeChildren().forEach((c) => c.destroy());
    labels.removeChildren().forEach((c) => c.destroy());
    cells.length = 0;
    const table = info.levels[level].table;
    for (let r = 0; r < rows; r++) {
      const row: Container[] = [];
      for (let i = 0; i < t; i++) {
        const c = new Container();
        c.position.set(X0 + i * (tile * 1.6 + gap), Y0 - r * (tile + gap));
        c.eventMode = 'static';
        c.cursor = 'pointer';
        c.on('pointertap', () => void pick(r, i));
        board.addChild(c);
        row.push(c);
      }
      cells.push(row);
      const bp = table[r];
      const lab = new Text({ text: bp ? times(bp) : '—', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '800', fontSize: Math.max(11, tile * 0.26), fill: 0xffe1a6, dropShadow: { color: 0, alpha: 0.8, blur: 6, distance: 0 } } });
      lab.anchor.set(1, 0.5);
      lab.position.set(X0 - tile * 0.8 - 10, Y0 - r * (tile + gap));
      labels.addChild(lab);
    }
    paint();
  };

  const paint = () => {
    const live = rowOf();
    cells.forEach((row, r) => row.forEach((c, i) => {
      const picked = round?.picked[r];
      const traps = round?.traps?.[r];
      const over = round && round.status !== 'open';
      let kind: 'hidden' | 'fruit' | 'snake' = 'hidden';
      if (picked === i) kind = traps?.includes(i) || (over && round!.status === 'busted' && r === round!.picked.length - 1) ? 'snake' : 'fruit';
      else if (over && traps) kind = traps.includes(i) ? 'snake' : 'fruit';
      face(c, kind, r === live, !!over && picked !== i);
    }));
  };

  const show = () => {
    const a = shell.action, open = round?.status === 'open';
    if (open && round!.picked.length > 0) {
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
  };

  const finish = async (v: TowersView) => {
    round = v;
    paint();
    shell.setBalance(v.balance);
    shell.setWin(v.win);
    if (v.status === 'cashed') { sound.win(v.multiplierBp >= 50000 ? 3 : 1); if (v.multiplierBp >= 100000) shell.bigWin(v.win, v.multiplierBp); }
    void refresh();
  };

  const pick = async (r: number, i: number) => {
    if (busy || round?.status !== 'open' || r !== rowOf()) return;
    busy = true;
    show();
    try {
      const v = await call<TowersView>('instant/towers/pick', { roundId: round.roundId, tile: i });
      const c = cells[r][i];
      await tweens.to(1, 0, 90, (k) => c.scale.set(k, 1));
      round = v;
      paint();
      await tweens.to(0, 1, 240, (k) => c.scale.set(k, Math.max(0.6, k)), ease.outBack);
      if (v.status === 'busted') { sound.stop(0); for (let s = 0; s < 6; s++) { board.x = (Math.random() - 0.5) * 10; await tweens.wait(30); } board.x = 0; }
      else sound.scatter(Math.min(v.picked.length, 6));
      if (v.status !== 'open') await finish(v);
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      busy = false;
      show();
    }
  };

  shell.action.onclick = async () => {
    if (busy) return;
    busy = true;
    show();
    try {
      if (round?.status === 'open') await finish(await call<TowersView>('instant/towers/cashout', { roundId: round.roundId }));
      else {
        round = await call<TowersView>('instant/towers/start', { stake: shell.stake, level, key: key() });
        shell.setBalance(round.balance);
        shell.setWin(0);
        paint();
        sound.spin();
      }
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      busy = false;
      show();
    }
  };

  let glow = 0;
  app.ticker.add((t: Ticker) => { glow += t.deltaTime * 0.08; const r = rowOf(); if (r >= 0) for (const c of cells[r] ?? []) c.alpha = 0.85 + Math.sin(glow) * 0.15; });
  app.renderer.on('resize', build);
  build();
  show();
};

boot().catch((e) => { document.body.innerHTML = `<p style="color:#eee;font-family:system-ui;padding:24px">${describe(e)}</p>`; });
