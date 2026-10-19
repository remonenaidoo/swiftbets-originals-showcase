import { Container, Graphics, Sprite, Text } from 'pixi.js';
import { sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times, kenoDraw } from '@originals/instant-kit';

interface KenoView { roundId: string; multiplierBp: number; win: number; detail: { picks: number[]; drawn: number[]; hits: number }; balance: number | null }
interface KenoInfo { rules: { numbers: number; drawn: number; tables: number[][] } }

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant<KenoInfo>({
    gameId: 'keno', title: 'Keno', accent: '#ffd23f', accent2: '#e08a12', background: '#0a0812', art: ['ball.webp'],
    verify: async (s, c, n) => `the draw was ${(await kenoDraw(s, c, n)).join(', ')}.`,
  });
  const tables = state.info.rules.tables;
  const picks = new Set<number>();
  let drawn: number[] = [], hits = new Set<number>(), busy = false;

  shell.controls.innerHTML = `
    <div class="row"><div class="label">Picks <b id="count" style="color:var(--ink)">0</b> / 10</div>
      <div class="chips" style="flex:0 0 auto"><button class="chip" id="k-rand">Random</button><button class="chip" id="k-clear">Clear</button></div></div>
    <div id="paytable" class="note" style="display:flex;gap:6px;overflow-x:auto;padding-bottom:2px"></div>`;
  const payRow = () => {
    const n = picks.size;
    document.getElementById('count')!.textContent = String(n);
    document.getElementById('paytable')!.innerHTML = n === 0 ? 'Pick numbers to see what they pay.'
      : tables[n].map((bp, h) => bp ? `<span style="flex:0 0 auto;padding:4px 8px;border-radius:9px;background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.08)"><b style="color:var(--accent)">${h}</b> hits ${times(bp, bp >= 100000 ? 0 : 2)}</span>` : '').join('');
    shell.action.disabled = busy || n === 0;
  };
  document.getElementById('k-clear')!.onclick = () => { picks.clear(); drawn = []; hits = new Set(); paint(); payRow(); };
  document.getElementById('k-rand')!.onclick = () => {
    picks.clear();
    while (picks.size < 10) picks.add(1 + Math.floor(Math.random() * 40));
    drawn = []; hits = new Set(); paint(); payRow();
  };

  shell.rules(`
    <h2>Keno</h2>
    <p>Pick 1 to 10 numbers from 40, then play: 10 numbers are drawn. The more of yours are drawn, the more you win; what each number of hits pays depends on how many you picked.</p>
    <div class="facts"><div><b>${Math.round(Math.max(...tables.flat()) / 10000).toLocaleString('en-ZA')}×</b><small>Top pay</small></div><div><b>10 of 40</b><small>Drawn</small></div></div>
    <h3>Pays</h3>
    <table class="table"><tr><th>Picks</th><th style="text-align:left">Hits → multiplier</th></tr>${tables.slice(1).map((row, i) => `<tr><td>${i + 1}</td><td style="text-align:left">${row.map((bp, h) => (bp ? `${h}: ${times(bp, bp >= 100000 ? 0 : 2)}` : '')).filter(Boolean).join(' · ')}</td></tr>`).join('')}</table>
    <p>Every row returns the same (each one proved exactly in the maths document).</p>
    <h3>Fairness</h3>
    <p>The draw is a shuffle of 1–40 by the round's floats from HMAC-SHA256(server seed, "client seed:nonce:cursor"); the first 10 are drawn. Change seeds to check any draw. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);

  // ---- Stage: the 40-number board and the drawn balls ----
  const tweens = new Tweens(app.ticker);
  const shade = new Graphics(), board = new Container(), tray = new Container();
  app.stage.addChild(shade, board, tray);
  const tiles: Container[] = [];
  let size = 50, W = 0, H = 0;
  const paint = () => tiles.forEach((t, i) => {
    const n = i + 1, picked = picks.has(n), wasDrawn = drawn.includes(n), hit = hits.has(n);
    t.removeChildren().forEach((c) => c.destroy());
    const fill = hit ? 0x36e2a0 : picked ? 0xffd23f : wasDrawn ? 0x3b6dff : 0x141024;
    const g = new Graphics().roundRect(-size / 2, -size / 2, size, size, size * 0.2).fill({ color: fill, alpha: picked || wasDrawn ? 0.95 : 0.78 })
      .roundRect(-size / 2, -size / 2, size, size, size * 0.2).stroke({ width: 2, color: hit ? 0xc8ffe9 : picked ? 0xfff2b8 : 0x6a5fa0, alpha: 0.9 });
    if (hit) g.circle(0, 0, size * 0.62).fill({ color: 0x36e2a0, alpha: 0.2 });
    const label = new Text({ text: String(n), style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: size * 0.4, fill: picked || hit ? 0x1a1206 : 0xf1edff } });
    label.anchor.set(0.5);
    t.addChild(g, label);
  });
  const build = () => {
    W = app.screen.width; H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x05030c, alpha: 0.5 });
    const cols = 8, rows = 5, trayH = Math.min(70, H * 0.14);
    size = Math.min((W - 20) / (cols + (cols - 1) * 0.14), (H - 50 - trayH) / (rows + (rows - 1) * 0.14));
    const gap = size * 0.14, bw = cols * size + (cols - 1) * gap, bh = rows * size + (rows - 1) * gap;
    const x0 = (W - bw) / 2 + size / 2, y0 = 44 + trayH + (H - 44 - trayH - bh) / 2 + size / 2;
    board.removeChildren();
    tiles.forEach((t, i) => { t.position.set(x0 + (i % cols) * (size + gap), y0 + Math.floor(i / cols) * (size + gap)); board.addChild(t); });
    tray.position.set(W / 2, 44 + trayH / 2);
    paint();
  };
  for (let i = 0; i < 40; i++) {
    const t = new Container();
    t.eventMode = 'static';
    t.cursor = 'pointer';
    t.on('pointertap', () => {
      if (busy) return;
      const n = i + 1;
      if (picks.has(n)) picks.delete(n); else if (picks.size < 10) picks.add(n);
      drawn = []; hits = new Set();
      sound.tick();
      paint(); payRow();
    });
    tiles.push(t);
  }
  build();
  app.renderer.on('resize', build);
  payRow();

  const ball = (n: number, i: number) => {
    const trayH = Math.min(70, H * 0.14), d = Math.min(trayH * 0.86, (W - 20) / 10.6);
    const c = new Container();
    const s = new Sprite(tex('ball.webp'));
    s.anchor.set(0.5);
    s.width = s.height = d;
    const band = new Graphics().circle(0, 0, d * 0.3).fill(picks.has(n) ? 0x36e2a0 : 0x3b6dff);
    const t = new Text({ text: String(n), style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: d * 0.32, fill: 0xffffff } });
    t.anchor.set(0.5);
    c.addChild(s, band, t);
    c.position.set((i - 4.5) * (d * 1.06), 0);
    tray.addChild(c);
    void tweens.to(0, 1, 260, (k) => c.scale.set(k), ease.outBack);
  };

  shell.action.textContent = 'PLAY';
  shell.action.onclick = async () => {
    if (busy || picks.size === 0) return;
    busy = true;
    shell.lock(true);
    payRow();
    tray.removeChildren().forEach((c) => c.destroy());
    drawn = []; hits = new Set(); paint();
    sound.spin();
    try {
      const v = await call<KenoView>('instant/keno/play', { stake: shell.stake, choices: { picks: [...picks] }, key: key() });
      for (const [i, n] of v.detail.drawn.entries()) {
        await tweens.wait(170);
        drawn.push(n);
        if (picks.has(n)) { hits.add(n); sound.scatter(Math.min(hits.size, 6)); } else sound.tick();
        ball(n, i);
        paint();
      }
      shell.setBalance(v.balance);
      shell.setWin(v.win);
      if (v.win > 0) { sound.win(v.multiplierBp >= 50000 ? 3 : 1); if (v.multiplierBp >= 100000) shell.bigWin(v.win, v.multiplierBp); }
      void refresh();
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      busy = false;
      shell.lock(false);
      payRow();
    }
  };
};

boot().catch((e) => { document.body.innerHTML = `<p style="color:#eee;font-family:system-ui;padding:24px">${describe(e)}</p>`; });
