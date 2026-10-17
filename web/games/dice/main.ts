import { Container, Graphics, Sprite, Text, type Ticker } from 'pixi.js';
import { rand, sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times } from '@originals/instant-kit';

interface DiceView { roundId: string; multiplierBp: number; win: number; detail: { roll: number; target: number; over: boolean; win: boolean }; balance: number | null }

const count = (target: number, over: boolean) => (over ? 9999 - target : target);
const payBp = (target: number, over: boolean) => Math.floor(96_000_000 / count(target, over));

const boot = async () => {
  const { shell, app, tex, refresh } = await bootInstant({
    gameId: 'dice', title: 'Dice', accent: '#ff5fd2', accent2: '#9b3cff', background: '#07040f', art: ['die.webp'],
    verify: async (s, c, n) => { const { floats } = await import('@originals/instant-kit'); const [f] = await floats(s, c, n, 1); return `the roll was ${(Math.floor(f * 10000) / 100).toFixed(2)}.`; },
  });
  let target = 5000, over = true, busy = false, roll: number | null = null;

  shell.controls.innerHTML = `
    <div class="row"><div class="label">Roll ${'<b id="dir-label">over</b>'} <b id="t-label">50.00</b></div><div class="chips" style="flex:0 0 auto"><button class="chip on" id="d-over">Over</button><button class="chip" id="d-under">Under</button></div></div>
    <input id="slider" type="range" min="2" max="98" step="0.01" value="50" style="width:100%;accent-color:#ff5fd2" aria-label="Target">
    <div class="row" style="font-size:13px;color:var(--muted)"><span>Multiplier <b id="m-out" style="color:var(--ink)"></b></span><span>Win chance <b id="c-out" style="color:var(--ink)"></b></span><span>Pays <b id="p-out" style="color:var(--accent)"></b></span></div>`;
  const slider = document.getElementById('slider') as HTMLInputElement;
  const readout = () => {
    target = Math.round(Number(slider.value) * 100);
    if (over) target = Math.min(target, 9799);
    document.getElementById('t-label')!.textContent = (target / 100).toFixed(2);
    document.getElementById('dir-label')!.textContent = over ? 'over' : 'under';
    document.getElementById('m-out')!.textContent = times(payBp(target, over), 4);
    document.getElementById('c-out')!.textContent = `${(count(target, over) / 100).toFixed(2)}%`;
    document.getElementById('p-out')!.textContent = rand(Math.floor(shell.stake * payBp(target, over) / 10000));
    draw();
  };
  slider.oninput = readout;
  const setDir = (o: boolean) => { over = o; document.getElementById('d-over')!.classList.toggle('on', o); document.getElementById('d-under')!.classList.toggle('on', !o); readout(); };
  document.getElementById('d-over')!.onclick = () => setDir(true);
  document.getElementById('d-under')!.onclick = () => setDir(false);
  for (const id of ['stake-up', 'stake-down']) document.getElementById(id)!.addEventListener('click', () => setTimeout(readout));

  shell.rules(`
    <h2>Dice</h2>
    <p>Choose a target and whether the roll will land over or under it, then roll. The roll is a number from 0.00 to 99.99. The lower your chance, the more it pays.</p>
    <div class="facts"><div><b>96.00%</b><small>Return to player</small></div><div><b>48×</b><small>Max multiplier</small></div><div><b>2–98%</b><small>Win chance</small></div></div>
    <h3>How it pays</h3>
    <p>With a win chance of c%, the pay is 96 ÷ c times your stake (floored to 0.0001×). A 50% chance pays 1.92×; a 10% chance pays 9.6×.</p>
    <h3>Fairness</h3>
    <p>The roll is the first float of HMAC-SHA256(server seed, "client seed:nonce:0") × 10,000, floored, ÷ 100. Change seeds to reveal the server seed and check any roll. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);

  // ---- Stage ----
  const tweens = new Tweens(app.ticker);
  const shade = new Graphics(), track = new Graphics(), labels = new Container(), marker = new Container();
  const die = new Sprite(tex('die.webp'));
  die.anchor.set(0.5);
  const big = new Text({ text: '50.00', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: 72, fill: 0xffffff, stroke: { color: 0x14062a, width: 6 }, dropShadow: { color: 0xff5fd2, alpha: 0.8, blur: 20, distance: 0 }, padding: 40 } });
  big.anchor.set(0.5);
  const flag = new Graphics();
  const flagText = new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: 16, fill: 0x14062a } });
  flagText.anchor.set(0.5);
  marker.addChild(flag, flagText);
  app.stage.addChild(shade, die, big, track, labels, marker);
  let W = 0, H = 0, X0 = 0, X1 = 0, TY = 0;
  const draw = () => {
    W = app.screen.width; H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x05030c, alpha: 0.35 });
    const s = Math.min(W, H);
    die.scale.set((s * 0.32) / die.texture.width);
    die.position.set(W / 2, H * 0.32);
    big.style.fontSize = Math.round(s * 0.13);
    big.position.set(W / 2, H * 0.58);
    X0 = W * 0.08; X1 = W * 0.92; TY = H * 0.8;
    const x = (v: number) => X0 + (X1 - X0) * v / 10000;
    track.clear().roundRect(X0 - 6, TY - 14, X1 - X0 + 12, 28, 14).fill({ color: 0x000000, alpha: 0.55 });
    const winFrom = over ? target : 0, winTo = over ? 10000 : target;
    track.roundRect(X0, TY - 6, X1 - X0, 12, 6).fill(0xff3d6e);
    track.roundRect(x(winFrom), TY - 6, x(winTo) - x(winFrom), 12, 6).fill(0x36e2a0);
    track.circle(x(target), TY, 11).fill(0xffffff).circle(x(target), TY, 11).stroke({ width: 3, color: 0x9b3cff });
    labels.removeChildren().forEach((c) => c.destroy());
    for (const v of [0, 25, 50, 75, 100]) {
      const t = new Text({ text: String(v), style: { fontSize: 12, fill: 0xcfd3f0, fontFamily: 'system-ui' } });
      t.anchor.set(0.5, 0);
      t.position.set(x(v * 100), TY + 18);
      labels.addChild(t);
    }
    marker.visible = roll !== null;
    if (roll !== null) marker.position.set(x(roll), TY - 30);
  };
  app.renderer.on('resize', draw);
  readout();

  let spin = 0;
  app.ticker.add((t: Ticker) => { die.rotation += spin * t.deltaTime; spin *= 0.95; die.y = H * 0.32 + Math.sin(performance.now() / 600) * 6; });

  shell.action.textContent = 'ROLL';
  shell.action.onclick = async () => {
    if (busy) return;
    busy = true;
    shell.action.disabled = true;
    shell.lock(true);
    sound.spin();
    spin = 0.5;
    try {
      const v = await call<DiceView>('instant/dice/play', { stake: shell.stake, choices: { target, over }, key: key() });
      const from = roll ?? 5000;
      // The number races to the roll; the flag slides along the track to it.
      await tweens.to(0, 1, 700, (k) => {
        const r = Math.round(from + (v.detail.roll - from) * k);
        big.text = (r / 100).toFixed(2);
        roll = r;
        draw();
      }, ease.outCubic);
      const won = v.detail.win;
      big.style.fill = won ? 0x6dffc4 : 0xff6b8b;
      flag.clear().roundRect(-34, -18, 68, 26, 8).fill(won ? 0x36e2a0 : 0xff3d6e).poly([-6, 8, 6, 8, 0, 16]).fill(won ? 0x36e2a0 : 0xff3d6e);
      flagText.text = (v.detail.roll / 100).toFixed(2);
      flagText.position.set(0, -5);
      shell.setBalance(v.balance);
      shell.setWin(v.win);
      if (won) { sound.win(v.multiplierBp >= 50000 ? 3 : 1); if (v.multiplierBp >= 100000) shell.bigWin(v.win, v.multiplierBp); } else sound.stop(0);
      setTimeout(() => { big.style.fill = 0xffffff; }, 900);
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
