import { Container, Graphics, Sprite, Text } from 'pixi.js';
import { assetUrl, rand, sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times, floats } from '@originals/instant-kit';

interface ScratchView { roundId: string; multiplierBp: number; win: number; detail: { prize: number; cells: number[] }; balance: number | null }
interface Prize { payBp: number; chance: number }
interface ScratchInfo { rules: { prizes: Prize[]; oddsScale: number } }

let bands: Prize[] = [];

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant<ScratchInfo>({
    gameId: 'lucky-scratch', title: 'Lucky Scratch', accent: '#3fe08f', accent2: '#e0b13f', background: '#06140c', art: ['p1.webp', 'p2.webp', 'p3.webp', 'p4.webp', 'p5.webp', 'p6.webp'],
    verify: async (s: string, c: string, n: number): Promise<string> => {
      const [f] = await floats(s, c, n, 1);
      let u = Math.floor(f * 100000), prize = 0;
      bands.some((p, i) => { if (u < p.chance) { prize = i + 1; return true; } u -= p.chance; return false; });
      return prize ? `the card won prize ${prize} (${times(bands[prize - 1].payBp)}).` : 'the card won no prize.';
    },
  });
  const prizes = state.info.rules.prizes;
  bands = prizes;
  let busy = false, card: ScratchView | null = null;
  const revealed = new Set<number>();

  shell.controls.innerHTML = `<div class="row" style="flex-wrap:wrap;gap:6px 14px;font-size:13px;color:var(--muted)">${prizes.map((p, i) =>
    `<span style="display:inline-flex;align-items:center;gap:6px"><img src="${assetUrl(`assets/lucky-scratch/p${i + 1}.webp`)}" alt="" style="width:26px;height:26px">×3 <b style="color:var(--ink)">${times(p.payBp)}</b></span>`).join('')}</div>
    <div class="row" style="font-size:13px;color:var(--muted)">Match three to win that prize. Tap a cell to scratch it, or press Scratch all.</div>`;

  shell.rules(`
    <h2>Lucky Scratch</h2>
    <p>Buy a card of nine cells and scratch it. Three of the same prize wins that prize; a card shows at most one set of three.</p>
    <div class="facts"><div><b>${times(prizes[5].payBp)}</b><small>Top prize</small></div><div><b>${((prizes.reduce((a, p) => a + p.chance, 0) / state.info.rules.oddsScale) * 100).toFixed(1)}%</b><small>Cards that win</small></div></div>
    <h3>Prizes and odds</h3>
    <table class="table"><tr><th>Prize</th><th>Pays</th><th>Chance</th></tr>${prizes.map((p, i) => `<tr><td>${i + 1}</td><td>${times(p.payBp)}</td><td>1 in ${Math.round(state.info.rules.oddsScale / p.chance).toLocaleString()}</td></tr>`).join('')}</table>
    <h3>Fairness</h3>
    <p>The card is decided before you scratch: the first float × 100,000 picks the prize from the bands above, and the next floats lay out the cells. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);

  // ---- Stage ----
  const tweens = new Tweens(app.ticker);
  const shade = new Graphics(), frame = new Graphics(), grid = new Container(), banner = new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: 30, fill: 0xffe58a, stroke: { color: 0x06140c, width: 5 }, dropShadow: { color: 0x3fe08f, alpha: 0.6, blur: 14, distance: 0 }, padding: 20 } });
  banner.anchor.set(0.5);
  app.stage.addChild(shade, frame, grid, banner);
  let cell = 90, X0 = 0, Y0 = 0;
  const foils: Graphics[] = [], icons: Sprite[] = [];

  const foil = (g: Graphics, k = 1) => {
    g.clear().roundRect(-cell / 2 + 4, -cell / 2 + 4, cell - 8, cell - 8, 12).fill({ color: 0xc9ced8, alpha: k });
    for (let i = 0; i < 6; i++) g.moveTo(-cell / 2 + 10 + i * cell * 0.15, cell / 2 - 10).lineTo(-cell / 2 + 10 + i * cell * 0.15 + cell * 0.3, -cell / 2 + 10).stroke({ width: 3, color: 0xffffff, alpha: 0.25 * k });
    g.roundRect(-cell / 2 + 4, -cell / 2 + 4, cell - 8, cell - 8, 12).stroke({ width: 2, color: 0x8b93a6, alpha: k });
  };

  const layout = () => {
    const W = app.screen.width, H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x020a05, alpha: 0.35 });
    cell = Math.min((W - 40) / 3, (H - 80) / 3);
    X0 = W / 2 - cell * 1.5; Y0 = 18 + (H - 70 - cell * 3) / 2;
    frame.clear().roundRect(X0 - 12, Y0 - 12, cell * 3 + 24, cell * 3 + 24, 20).fill({ color: 0x0b2416, alpha: 0.85 }).roundRect(X0 - 12, Y0 - 12, cell * 3 + 24, cell * 3 + 24, 20).stroke({ width: 3, color: 0xe0b13f });
    grid.removeChildren().forEach((c) => c.destroy());
    foils.length = 0; icons.length = 0;
    for (let i = 0; i < 9; i++) {
      const holder = new Container();
      holder.position.set(X0 + (i % 3) * cell + cell / 2, Y0 + Math.floor(i / 3) * cell + cell / 2);
      holder.addChild(new Graphics().roundRect(-cell / 2 + 4, -cell / 2 + 4, cell - 8, cell - 8, 12).fill({ color: 0x103322, alpha: 0.9 }));
      const icon = new Sprite(tex(`p${card?.detail.cells[i] ?? 1}.webp`));
      icon.anchor.set(0.5);
      icon.width = icon.height = cell * 0.74;
      icon.visible = !!card;
      holder.addChild(icon);
      const f = new Graphics();
      foil(f, card && revealed.has(i) ? 0 : 1);
      holder.addChild(f);
      holder.eventMode = 'static';
      holder.cursor = 'pointer';
      holder.on('pointertap', () => void scratch(i));
      grid.addChild(holder);
      foils.push(f); icons.push(icon);
    }
    banner.position.set(W / 2, Math.min(H - 26, Y0 + cell * 3 + 40));
    banner.style.fontSize = Math.round(Math.min(30, W * 0.07));
  };
  app.renderer.on('resize', layout);
  layout();

  const scratch = async (i: number) => {
    if (!card || revealed.has(i)) return;
    revealed.add(i);
    sound.tick();
    await tweens.to(1, 0, 260, (k) => foil(foils[i], k), ease.outCubic);
    if (revealed.size === 9) await settle();
  };

  const settle = async () => {
    if (!card) return;
    const v = card;
    if (v.detail.prize) {
      for (let i = 0; i < 9; i++) if (v.detail.cells[i] === v.detail.prize) void tweens.to(0, 1, 600, (k) => icons[i].scale.set((cell * 0.74 / icons[i].texture.width) * (1 + Math.sin(k * Math.PI) * 0.18)), ease.outCubic);
      banner.text = `Match! ${times(v.multiplierBp)} · ${rand(v.win)}`;
      banner.style.fill = 0xffe58a;
      sound.win(v.multiplierBp >= 500000 ? 3 : 1);
      if (v.multiplierBp >= 100000) shell.bigWin(v.win, v.multiplierBp);
    } else {
      banner.text = 'No match this time';
      banner.style.fill = 0xb9c9bf;
      sound.stop(0);
    }
    shell.setWin(v.win);
    shell.setBalance(v.balance);
    shell.action.textContent = 'BUY CARD';
    shell.action.disabled = false;
    shell.lock(false);
    busy = false;
    void refresh();
  };

  shell.action.textContent = 'BUY CARD';
  shell.action.onclick = async () => {
    if (card && revealed.size < 9) {
      // Scratch all: the rest come off one after another.
      shell.action.disabled = true;
      for (let i = 0; i < 9; i++) if (!revealed.has(i)) { await scratch(i); await tweens.wait(90); }
      return;
    }
    if (busy) return;
    busy = true;
    shell.action.disabled = true;
    shell.lock(true);
    banner.text = '';
    try {
      const v = await call<ScratchView>('instant/lucky-scratch/play', { stake: shell.stake, choices: {}, key: key() });
      card = v;
      revealed.clear();
      // The stake is taken now; the win is shown (and the balance moves) as the card is scratched.
      shell.setWin(0);
      layout();
      sound.spin();
      shell.action.textContent = 'SCRATCH ALL';
      shell.action.disabled = false;
    } catch (e) {
      shell.toast(describe(e));
      busy = false;
      shell.action.disabled = false;
      shell.lock(false);
    }
  };
};

boot().catch((e) => { document.body.innerHTML = `<p style="color:#eee;font-family:system-ui;padding:24px">${describe(e)}</p>`; });
