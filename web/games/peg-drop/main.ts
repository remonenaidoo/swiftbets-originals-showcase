import { Container, Graphics, Sprite, Text, type Ticker } from 'pixi.js';
import { sound } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times, floats } from '@originals/instant-kit';

interface PegView { roundId: string; multiplierBp: number; win: number; detail: { rows: number; risk: string; path: number[]; bucket: number }; balance: number | null }
interface PegInfo { rules: { rows: number[]; risks: string[]; tables: Record<string, number[]> } }

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant<PegInfo>({
    gameId: 'peg-drop', title: 'Peg Drop', accent: '#ffb347', accent2: '#e0602a', background: '#0b0618', art: ['ball.webp'],
    verify: async (s, c, n) => {
      const r = Number((document.getElementById('verify-rows') as HTMLInputElement | null)?.value ?? 16);
      const f = await floats(s, c, n, r);
      const path: number[] = f.map((x) => (x < 0.5 ? 0 : 1));
      return `with ${r} rows the ball bounced ${path.map((p) => (p ? 'R' : 'L')).join('')} into bucket ${path.reduce((a, b) => a + b, 0) + 1} of ${r + 1}.`;
    },
  });
  const tables = state.info.rules.tables;
  let rows = 16, risk = 'medium';
  let flying = 0;

  shell.controls.innerHTML = `
    <div class="label">Rows</div><div class="chips" id="rows">${[8, 12, 16].map((r) => `<button class="chip${r === rows ? ' on' : ''}" data-r="${r}">${r}</button>`).join('')}</div>
    <div class="label">Risk</div><div class="chips" id="risk">${['low', 'medium', 'high'].map((r) => `<button class="chip${r === risk ? ' on' : ''}" data-k="${r}">${r[0].toUpperCase() + r.slice(1)}</button>`).join('')}</div>`;
  for (const b of document.querySelectorAll<HTMLElement>('#rows .chip')) b.onclick = () => { if (flying) return; rows = Number(b.dataset.r); for (const x of document.querySelectorAll('#rows .chip')) x.classList.toggle('on', x === b); build(); };
  for (const b of document.querySelectorAll<HTMLElement>('#risk .chip')) b.onclick = () => { if (flying) return; risk = b.dataset.k!; for (const x of document.querySelectorAll('#risk .chip')) x.classList.toggle('on', x === b); build(); rules(); };

  const rules = () => shell.rules(`
    <h2>Peg Drop</h2>
    <p>Drop a ball through the pegs. At every row it bounces left or right with equal chance, and it pays the multiplier of the bucket it lands in. More rows and higher risk spread the pays further: bigger edges, smaller middle.</p>
    <div class="facts"><div><b>${(Math.max(...tables['16-high']) / 10000).toLocaleString('en-ZA')}×</b><small>Top bucket</small></div><div><b>8–16</b><small>Rows</small></div></div>
    <h3>Pays: ${rows} rows, ${risk} risk</h3>
    <p class="note">${tables[`${rows}-${risk}`].map((b) => times(b, b >= 100000 ? 0 : 2)).join(' · ')}</p>
    <p>Every table returns the same (each one proved exactly in the maths document).</p>
    <h3>Fairness</h3>
    <p>Each bounce is one float of HMAC-SHA256(server seed, "client seed:nonce:cursor"): right when it is at least 0.5. Change seeds to check any drop (enter its rows: <input id="verify-rows" type="number" value="16" min="8" max="16" step="4" style="width:56px">). In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);
  rules();

  // ---- Stage ----
  const shade = new Graphics(), pegs = new Graphics(), buckets = new Container(), balls = new Container(), fx = new Container();
  pegs.blendMode = 'add';
  app.stage.addChild(shade, pegs, buckets, fx, balls);
  let W = 0, H = 0, cx = 0, y0 = 0, dx = 0, dy = 0, by = 0;
  const bucketBoxes: Container[] = [];
  const build = () => {
    W = app.screen.width; H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x05030c, alpha: 0.3 });
    const top = 64, bottom = H - 16;
    const bw = Math.min(W * 0.96, (bottom - top) * 1.15);
    dx = bw / (rows + 2);
    dy = Math.min(dx * 0.9, (bottom - top - dx * 1.2) / rows);
    cx = W / 2; y0 = top + dy * 0.5;
    pegs.clear();
    for (let r = 0; r < rows; r++) {
      for (let j = 0; j < r + 3; j++) {
        const x = cx + (j - (r + 2) / 2) * dx, y = y0 + r * dy;
        pegs.circle(x, y, Math.max(2.2, dx * 0.11)).fill({ color: 0xffe0b0, alpha: 0.95 });
        pegs.circle(x, y, Math.max(4, dx * 0.24)).fill({ color: 0xffa040, alpha: 0.12 });
      }
    }
    by = y0 + rows * dy - dy * 0.15;
    buckets.removeChildren().forEach((c) => c.destroy());
    bucketBoxes.length = 0;
    const table = tables[`${rows}-${risk}`];
    const max = Math.max(...table);
    table.forEach((bp, k) => {
      const box = new Container();
      const heat = Math.log(bp / 10000 + 1) / Math.log(max / 10000 + 1);
      const colour = heat > 0.66 ? 0xff3d6e : heat > 0.4 ? 0xff7a3d : heat > 0.2 ? 0xffb347 : 0xffd96b;
      const w = dx * 0.92, h = Math.max(20, dx * 0.62);
      box.addChild(new Graphics().roundRect(-w / 2, 0, w, h, Math.min(8, w * 0.2)).fill(colour).roundRect(-w / 2, 0, w, h * 0.45, Math.min(8, w * 0.2)).fill({ color: 0xffffff, alpha: 0.18 }));
      const t = new Text({ text: bp >= 1000000 ? `${Math.round(bp / 10000)}` : (bp / 10000).toFixed(bp >= 100000 ? 0 : 1), style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: Math.max(9, Math.min(15, w * 0.32)), fill: 0x2a0c08 } });
      t.anchor.set(0.5);
      t.position.set(0, h / 2);
      box.addChild(t);
      box.position.set(cx + (k - rows / 2) * dx, by);
      buckets.addChild(box);
      bucketBoxes.push(box);
    });
  };
  build();
  app.renderer.on('resize', build);

  interface Ball { s: Sprite; path: number[]; t: number; done: () => void }
  const live: Ball[] = [];
  const at = (path: number[], i: number) => {
    const rights = path.slice(0, i).reduce((a, b) => a + b, 0);
    return { x: cx + (rights - i / 2) * dx, y: y0 + i * dy - dy * 0.55 };
  };
  app.ticker.add((tk: Ticker) => {
    for (const b of [...live]) {
      b.t += tk.deltaMS / 95; // one row every 95 ms
      const i = Math.min(Math.floor(b.t), b.path.length), k = Math.min(1, b.t - i);
      const p0 = at(b.path, i), p1 = at(b.path, Math.min(i + 1, b.path.length));
      // A little hop between pegs: along the line, plus a bounce arc.
      b.s.position.set(p0.x + (p1.x - p0.x) * k, p0.y + (p1.y - p0.y) * k - Math.sin(k * Math.PI) * dy * 0.35);
      if (i >= b.path.length) { live.splice(live.indexOf(b), 1); b.done(); }
    }
  });

  const drop = (v: PegView) => new Promise<void>((done) => {
    const s = new Sprite(tex('ball.webp'));
    s.anchor.set(0.5);
    s.scale.set((dx * 0.55) / s.texture.width);
    balls.addChild(s);
    live.push({ s, path: v.detail.path, t: 0, done: () => {
      s.destroy();
      const box = bucketBoxes[v.detail.bucket];
      if (box) { box.y = by + 6; setTimeout(() => { box.y = by; }, 140); }
      done();
    } });
  });

  shell.action.textContent = 'DROP';
  shell.action.onclick = async () => {
    flying++;
    shell.lock(true);
    sound.spin();
    try {
      const v = await call<PegView>('instant/peg-drop/play', { stake: shell.stake, choices: { rows, risk }, key: key() });
      await drop(v);
      shell.setBalance(v.balance);
      shell.setWin(v.win);
      if (v.multiplierBp >= 10000) sound.win(v.multiplierBp >= 100000 ? 3 : 1); else sound.tick();
      if (v.multiplierBp >= 100000) shell.bigWin(v.win, v.multiplierBp);
      void refresh();
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      flying--;
      if (!flying) shell.lock(false);
    }
  };
};

boot().catch((e) => { document.body.innerHTML = `<p style="color:#eee;font-family:system-ui;padding:24px">${describe(e)}</p>`; });
