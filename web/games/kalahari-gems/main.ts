import { Container, Graphics, Sprite, Text, type Ticker } from 'pixi.js';
import { rand, sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key } from '@originals/instant-kit';

interface GemCell { symbol: string; multiplier: number }
interface GemWin { symbol: string; count: number; units: number; cells: [number, number][] }
interface GemStep { wins: GemWin[]; board: GemCell[][] }
interface GemSpin { free: boolean; stops: number[]; board: GemCell[][]; steps: GemStep[]; suns: number; multiplier: number; units: number; paid: number }
interface GemsView { roundId: string; multiplierBp: number; win: number; detail: { spins: GemSpin[]; units: number; capped: boolean; freeSpins: number }; balance: number | null }
interface GemsInfo { rules: { units: number; pays: Record<string, number[]>; sunStakes: number[]; meteors: { value: number; weight: number }[] } }

const NAMES = ['Sunstone', 'Tanzanite', 'TigersEye', 'DesertRose', 'Quartz', 'Agate', 'Jasper', 'Amethyst', 'Opal', 'Sun', 'Meteor'];
const file = (s: string) => `symbols/${s.toLowerCase()}.webp`;

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant<GemsInfo>({
    gameId: 'kalahari-gems', title: 'Kalahari Gem Fall', accent: '#ff9a3d', accent2: '#c2410c', background: '#120612', art: [...NAMES.map(file), 'logo.webp'],
    verify: async () => 'replay the round from its floats: each reel stops at floor(float × 100); the full rules are in the maths document.',
  });
  const units = state.info.rules.units;
  let busy = false;

  shell.rules(`
    <h2>Kalahari Gem Fall</h2>
    <p>6 reels, 5 rows. A gem pays anywhere on the board when 8 or more of it show. Winning gems vanish and the gems above fall into their place, with new ones dropping in from above; this goes on while anything pays.</p>
    <div class="facts"><div><b>96.00%</b><small>Return to player</small></div><div><b>5,000×</b><small>Max win</small></div><div><b>8+</b><small>To win</small></div></div>
    <h3>Pays at ${rand(shell.stake)} stake (8–9 · 10–11 · 12+)</h3>
    <table class="table">${Object.entries(state.info.rules.pays).map(([s, p]) => `<tr><td>${s.replace('TigersEye', "Tiger's Eye").replace('DesertRose', 'Desert Rose')}</td>${p.map((u) => `<td>${rand(Math.floor(u * shell.stake / units))}</td>`).join('')}</tr>`).join('')}</table>
    <h3>Suns and free spins</h3>
    <p>4, 5 or 6 Suns pay ${state.info.rules.sunStakes.join(', ')}× the stake and award 10 free spins (3 or more in free spins add 5, up to 60). In free spins, meteors land carrying ×2 to ×50; they stay through the tumbles, and when nothing more pays the spin's win is multiplied by their total.</p>
    <h3>How wins pay</h3>
    <p>Every tumble's wins are added. A round pays at most 5,000× the stake. Every round is played in full on the server from your provably fair seeds before it is shown. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);

  // ---- Board ----
  const tweens = new Tweens(app.ticker);
  const shade = new Graphics(), frame = new Graphics(), board = new Container(), fx = new Container();
  const logo = new Sprite(tex('logo.webp'));
  logo.anchor.set(0.5, 0);
  const banner = new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: 20, fill: 0xffe0b0, stroke: { color: 0x1a0710, width: 5 }, padding: 10 } });
  banner.anchor.set(0.5);
  const amount = new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: 54, fill: 0xffe08a, stroke: { color: 0x3a1a00, width: 7 }, dropShadow: { color: 0, alpha: 0.7, blur: 10, distance: 3 }, padding: 30 } });
  amount.anchor.set(0.5);
  app.stage.addChild(shade, logo, frame, board, fx, banner, amount);
  let cell = 60, X0 = 0, Y0 = 0;
  let sprites: Container[][] = [];
  const at = (r: number, row: number) => ({ x: X0 + r * cell + cell / 2, y: Y0 + row * cell + cell / 2 });
  const make = (c: GemCell) => {
    const holder = new Container();
    const s = new Sprite(tex(file(c.symbol)));
    s.anchor.set(0.5);
    s.width = s.height = cell * 0.9;
    holder.addChild(s);
    if (c.symbol === 'Meteor' && c.multiplier) {
      const t = new Text({ text: `×${c.multiplier}`, style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: cell * 0.3, fill: 0xffffff, stroke: { color: 0x5a1000, width: 5 }, padding: 6 } });
      t.anchor.set(0.5);
      t.y = cell * 0.22;
      holder.addChild(t);
    }
    board.addChild(holder);
    return holder;
  };
  const layout = () => {
    const W = app.screen.width, H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x0a0310, alpha: 0.35 });
    const logoH = Math.min(H * 0.13, 90);
    logo.scale.set(Math.min(logoH / logo.texture.height, (W * 0.5) / logo.texture.width));
    logo.position.set(W / 2, 34);
    const top = 34 + logo.height + 8;
    cell = Math.floor(Math.min((W - 16) / 6, (H - top - 40) / 5));
    X0 = (W - cell * 6) / 2; Y0 = top + (H - top - 40 - cell * 5) / 2;
    frame.clear().roundRect(X0 - 8, Y0 - 8, cell * 6 + 16, cell * 5 + 16, 16).fill({ color: 0x14060f, alpha: 0.72 }).roundRect(X0 - 8, Y0 - 8, cell * 6 + 16, cell * 5 + 16, 16).stroke({ width: 3, color: 0xff9a3d, alpha: 0.85 });
    banner.position.set(W / 2, Y0 + cell * 5 + 26);
    amount.position.set(W / 2, Y0 + cell * 2.5);
    sprites.forEach((col, r) => col.forEach((s, row) => { const p = at(r, row); s.position.set(p.x, p.y); }));
  };
  app.renderer.on('resize', layout);
  layout();

  const show = (b: GemCell[][]) => {
    board.removeChildren().forEach((c) => c.destroy({ children: true }));
    sprites = b.map((col, r) => col.map((c, row) => { const s = make(c); const p = at(r, row); s.position.set(p.x, p.y); return s; }));
  };
  // Something to look at before the first spin.
  show(Array.from({ length: 6 }, () => Array.from({ length: 5 }, () => ({ symbol: NAMES[Math.floor(Math.random() * 9)], multiplier: 0 }))));

  /** Drops a whole board in from above, reel by reel. */
  const dropIn = async (b: GemCell[][]) => {
    await Promise.all(sprites.flat().map((s, i) => tweens.to(s.y, s.y + cell * 6, 160 + (i % 5) * 20, (v) => (s.y = v), ease.linear)));
    show(b);
    await Promise.all(sprites.map((col, r) => Promise.all(col.map((s, row) => {
      const target = at(r, row).y;
      s.y = target - cell * 6;
      return tweens.wait(r * 60).then(() => tweens.to(target - cell * 6, target, 300, (v) => (s.y = v), ease.outBack));
    }))));
    sound.stop(2);
  };

  interface P { g: Graphics; vx: number; vy: number; life: number }
  const parts: P[] = [];
  const burst = (x: number, y: number, colour: number) => {
    for (let i = 0; i < 12; i++) {
      const g = new Graphics().circle(0, 0, 2 + Math.random() * 3).fill(colour);
      g.blendMode = 'add';
      g.position.set(x, y);
      fx.addChild(g);
      const a = Math.random() * Math.PI * 2, v = 2 + Math.random() * 4;
      parts.push({ g, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: 0 });
    }
  };
  app.ticker.add((t: Ticker) => {
    for (const p of [...parts]) {
      p.life += t.deltaTime; p.g.x += p.vx * t.deltaTime; p.g.y += p.vy * t.deltaTime; p.vy += 0.15 * t.deltaTime; p.g.alpha = Math.max(0, 1 - p.life / 40);
      if (p.life > 40) { p.g.destroy(); parts.splice(parts.indexOf(p), 1); }
    }
  });

  /** One tumble: the winners glow and vanish, survivors fall, new gems drop in from above. */
  const tumble = async (step: GemStep, won: number) => {
    const removed = new Set(step.wins.flatMap((w) => w.cells.map(([r, row]) => `${r}:${row}`)));
    amount.text = rand(won);
    amount.visible = true;
    sound.win(1);
    for (const k of removed) {
      const [r, row] = k.split(':').map(Number);
      const p = at(r, row);
      burst(p.x, p.y, 0xffc46b);
    }
    await tweens.to(1, 1.25, 260, (v) => { for (const k of removed) { const [r, row] = k.split(':').map(Number); sprites[r][row].scale.set(v); } }, ease.outBack);
    await tweens.to(1, 0, 200, (v) => { for (const k of removed) { const [r, row] = k.split(':').map(Number); sprites[r][row].alpha = v; } });
    amount.visible = false;
    const next: Container[][] = [];
    const moves: Promise<void>[] = [];
    for (let r = 0; r < 6; r++) {
      const survivors = sprites[r].filter((_, row) => !removed.has(`${r}:${row}`));
      for (let row = 0; row < 5; row++) if (removed.has(`${r}:${row}`)) sprites[r][row].destroy({ children: true });
      const k = 5 - survivors.length;
      const fresh = step.board[r].slice(0, k).map((c, i) => { const s = make(c); s.position.set(at(r, i).x, at(r, i).y - cell * (k + 1)); return s; });
      const col = [...fresh, ...survivors];
      col.forEach((s, row) => { const target = at(r, row).y; if (s.y !== target) moves.push(tweens.to(s.y, target, 320, (v) => (s.y = v), ease.outBack)); });
      next.push(col);
    }
    sprites = next;
    await Promise.all(moves);
    sound.stop(3);
  };

  const playSpin = async (spin: GemSpin, st: number, running: { won: number }) => {
    await dropIn(spin.board);
    let spinWon = 0;
    for (const step of spin.steps) {
      const stepWon = step.wins.reduce((a, w) => a + Math.floor(w.units * st / units), 0);
      spinWon += stepWon;
      await tumble(step, spinWon);
    }
    if (spin.free && spin.multiplier > 0 && spinWon > 0) {
      // The meteors' total multiplies the spin's win.
      amount.text = `${rand(spinWon)} × ${spin.multiplier}`;
      amount.visible = true;
      sound.big();
      await tweens.wait(900);
    }
    const paid = Math.floor(spin.paid * st / units);
    if (paid > 0) {
      amount.text = rand(paid);
      amount.visible = true;
      running.won += paid;
      shell.setWin(running.won);
      await tweens.wait(500);
      amount.visible = false;
    }
  };

  shell.action.textContent = 'SPIN';
  shell.action.onclick = async () => {
    if (busy) return;
    busy = true;
    shell.action.disabled = true;
    shell.lock(true);
    shell.setWin(0);
    sound.spin();
    const st = shell.stake;
    try {
      const v = await call<GemsView>('instant/kalahari-gems/play', { stake: st, choices: {}, key: key() });
      shell.setBalance(v.balance === null ? null : v.balance - v.win);
      const running = { won: 0 };
      const [first, ...free] = v.detail.spins;
      await playSpin(first, st, running);
      if (free.length) {
        sound.feature();
        banner.text = 'FREE SPINS';
        await tweens.wait(900);
        for (const [i, spin] of free.entries()) {
          banner.text = `FREE SPIN ${i + 1} of ${free.length}  ·  won ${rand(running.won)}`;
          await playSpin(spin, st, running);
        }
        banner.text = `FREE SPINS won ${rand(running.won - Math.floor(first.paid * st / units))}`;
      }
      shell.setWin(v.win);
      shell.setBalance(v.balance);
      if (v.multiplierBp >= 100000) shell.bigWin(v.win, v.multiplierBp);
      if (v.detail.capped) shell.toast('Maximum win reached: 5,000× stake');
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
