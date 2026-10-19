import { Container, Graphics, Sprite, Text, type Ticker } from 'pixi.js';
import { rand, sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times, floats } from '@originals/instant-kit';

interface GemView { roundId: string; multiplierBp: number; win: number; detail: { gems: number[]; hand: string }; balance: number | null }
interface GemInfo { rules: { colours: number; pays: Record<string, number> } }

const HANDS: [string, string][] = [['five', 'Five of a kind'], ['four', 'Four of a kind'], ['full-house', 'Full house'], ['three', 'Three of a kind'], ['two-pair', 'Two pair'], ['pair', 'Pair']];
const GLOW = [0xff4d6a, 0xff9a3d, 0xffd84d, 0x4dff9a, 0x4de3ff, 0x4d7bff, 0xb04dff];

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant<GemInfo>({
    gameId: 'gem-hand', title: 'Gem Hand', accent: '#b04dff', accent2: '#4de3ff', background: '#0c0614', art: Array.from({ length: 7 }, (_, i) => `gem-${i + 1}.webp`),
    verify: async (s, c, n) => { const f = await floats(s, c, n, 5); return `the gems were colours ${f.map((x) => Math.floor(x * 7) + 1).join(', ')} (1–7).`; },
  });
  const pays = state.info.rules.pays;
  let busy = false;

  shell.controls.innerHTML = `<div id="table" style="display:grid;grid-template-columns:1fr auto auto;gap:4px 14px;font-size:13px;color:var(--muted)"></div>`;
  const table = (hit?: string) => {
    document.getElementById('table')!.innerHTML = HANDS.map(([k, name]) => {
      const on = k === hit;
      return `<span style="${on ? 'color:var(--ink);font-weight:800' : ''}">${name}</span><b style="color:${on ? 'var(--accent)' : 'var(--ink)'}">${times(pays[k])}</b><span style="${on ? 'color:var(--accent)' : ''}">${rand(Math.floor((shell.stake * pays[k]) / 10000))}</span>`;
    }).join('');
  };
  table();
  for (const id of ['stake-up', 'stake-down']) document.getElementById(id)!.addEventListener('click', () => setTimeout(() => table()));

  shell.rules(`
    <h2>Gem Hand</h2>
    <p>Five gems fall, each one of seven colours. Matching colours make a hand, paid like poker: the more that match, the more it pays.</p>
    <div class="facts"><div><b>${state.info.rtp.toFixed(2)}%</b><small>Return to player</small></div><div><b>${times(pays.five)}</b><small>Five of a kind</small></div><div><b>7</b><small>Colours</small></div></div>
    <h3>Pays</h3>
    <table class="table">${HANDS.map(([k, name]) => `<tr><td>${name}</td><td>${times(pays[k])}</td></tr>`).join('')}<tr><td>Five different</td><td>—</td></tr></table>
    <p>Each gem is one of 7 colours with equal chance, so each of the 16,807 hands is equally likely.</p>
    <h3>Fairness</h3>
    <p>Each gem is one float of HMAC-SHA256(server seed, "client seed:nonce:0") × 7, floored. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);

  // ---- Stage ----
  const tweens = new Tweens(app.ticker);
  const shade = new Graphics(), slots = new Graphics(), gems = new Container(), fx = new Graphics();
  const label = new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: 32, fill: 0xffffff, stroke: { color: 0x0c0614, width: 5 }, dropShadow: { color: 0xb04dff, alpha: 0.7, blur: 14, distance: 0 }, padding: 22 } });
  label.anchor.set(0.5);
  app.stage.addChild(shade, slots, fx, gems, label);
  let size = 70, X0 = 0, Y = 0, shown: number[] = [0, 2, 4, 3, 6], hit: number[] = [];
  const sprites: Sprite[] = [];

  const layout = () => {
    const W = app.screen.width, H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x06030b, alpha: 0.35 });
    size = Math.min((W - 30) / 5.4, (H - 90) * 0.55);
    X0 = W / 2 - size * 2.2; Y = 20 + (H - 70) / 2;
    slots.clear();
    for (let i = 0; i < 5; i++) slots.roundRect(X0 + i * size * 1.1 - size * 0.5, Y - size * 0.62, size, size * 1.24, 18).fill({ color: 0x1a0d2a, alpha: 0.8 }).roundRect(X0 + i * size * 1.1 - size * 0.5, Y - size * 0.62, size, size * 1.24, 18).stroke({ width: 2, color: 0x6b3fa0, alpha: 0.8 });
    gems.removeChildren().forEach((c) => c.destroy());
    sprites.length = 0;
    shown.forEach((g, i) => {
      const s = new Sprite(tex(`gem-${g + 1}.webp`));
      s.anchor.set(0.5);
      s.width = s.height = size * 0.86;
      s.position.set(X0 + i * size * 1.1, Y);
      s.alpha = hit.length && !hit.includes(i) ? 0.35 : 1;
      gems.addChild(s);
      sprites.push(s);
    });
    label.position.set(W / 2, Math.min(H - 28, Y + size * 0.62 + 36));
  };
  app.renderer.on('resize', layout);
  layout();

  let t0 = 0;
  app.ticker.add((t: Ticker) => {
    t0 += t.deltaTime * 0.05;
    fx.clear();
    if (!busy) hit.forEach((i) => fx.circle(X0 + i * size * 1.1, Y, size * (0.5 + Math.sin(t0 + i) * 0.04)).fill({ color: GLOW[shown[i]], alpha: 0.22 }));
  });

  shell.action.textContent = 'DEAL';
  shell.action.onclick = async () => {
    if (busy) return;
    busy = true;
    shell.action.disabled = true;
    shell.lock(true);
    label.text = ''; hit = [];
    sound.spin();
    try {
      const v = await call<GemView>('instant/gem-hand/play', { stake: shell.stake, choices: {}, key: key() });
      shown = v.detail.gems;
      layout();
      // The gems drop in one by one with a little bounce.
      for (let i = 0; i < 5; i++) {
        const s = sprites[i], y = s.y;
        s.y = -size;
        s.alpha = 1;
        await tweens.to(0, 1, 260, (k) => { s.y = -size + (y + size) * k; }, ease.outBack);
        sound.tick();
      }
      const counts = new Map<number, number>();
      v.detail.gems.forEach((g) => counts.set(g, (counts.get(g) ?? 0) + 1));
      hit = v.detail.gems.map((g, i) => ((counts.get(g) ?? 0) >= 2 ? i : -1)).filter((i) => i >= 0);
      layout();
      const name = HANDS.find(([k]) => k === v.detail.hand)?.[1];
      label.text = name ? `${name} · ${times(v.multiplierBp)}` : 'No hand';
      label.style.fill = v.multiplierBp >= 10000 ? 0xffe58a : v.multiplierBp ? 0xd8c4ff : 0xa89cba;
      table(v.detail.hand);
      shell.setBalance(v.balance);
      shell.setWin(v.win);
      if (v.multiplierBp) { sound.win(v.multiplierBp >= 50000 ? 3 : 1); if (v.multiplierBp >= 100000) shell.bigWin(v.win, v.multiplierBp); } else sound.stop(0);
      void refresh();
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      busy = false;
      shell.action.disabled = false;
      shell.lock(false);
    }
  };
};

boot().catch((e) => { document.body.innerHTML = `<p style="color:#eee;font-family:system-ui;padding:24px">${describe(e)}</p>`; });
