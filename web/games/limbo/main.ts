import { Graphics, Sprite, Text, type Ticker } from 'pixi.js';
import { rand, sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times, floats } from '@originals/instant-kit';

interface LimboView { roundId: string; multiplierBp: number; win: number; detail: { result: number; target: number; win: boolean }; balance: number | null }

const STEPS = [10100, 11000, 12500, 15000, 20000, 30000, 50000, 100000, 250000, 1000000, 10000000, 50000000];

let rateBp = 9600;

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant({
    gameId: 'limbo', title: 'Limbo', accent: '#ffc24d', accent2: '#e0731a', background: '#08051a', art: ['comet.webp'],
    verify: async (s, c, n) => {
      const [u] = await floats(s, c, n, 1);
      return `the result was ${times(Math.min(Math.max(Math.floor(rateBp / 100 / (1 - u)), 100), 500000) * 100)}.`;
    },
  });
  rateBp = state.info.rateBp;
  let target = 20000, busy = false;

  shell.controls.innerHTML = `
    <div class="label">Target multiplier</div>
    <div class="field"><button id="t-down" aria-label="Lower target">−</button><input id="t-val" inputmode="decimal" value="2.00" aria-label="Target"><button id="t-up" aria-label="Raise target">+</button></div>
    <div class="row" style="font-size:13px;color:var(--muted)"><span>Win chance <b id="c-out" style="color:var(--ink)"></b></span><span>Pays <b id="p-out" style="color:var(--accent)"></b></span></div>`;
  const input = document.getElementById('t-val') as HTMLInputElement;
  const set = (bp: number) => {
    target = Math.min(50_000_000, Math.max(10_100, Math.round(bp / 100) * 100));
    input.value = (target / 10000).toFixed(2);
    document.getElementById('c-out')!.textContent = `${(rateBp / 100 / (target / 10000)).toFixed(4)}%`;
    document.getElementById('p-out')!.textContent = rand(Math.floor(shell.stake * target / 10000));
    goal.text = `Target ${times(target)}`;
  };
  document.getElementById('t-down')!.onclick = () => set([...STEPS].reverse().find((s) => s < target) ?? STEPS[0]);
  document.getElementById('t-up')!.onclick = () => set(STEPS.find((s) => s > target) ?? target);
  input.onchange = () => set(Number(input.value.replace(',', '.')) * 10000 || 20000);
  for (const id of ['stake-up', 'stake-down']) document.getElementById(id)!.addEventListener('click', () => setTimeout(() => set(target)));

  shell.rules(`
    <h2>Limbo</h2>
    <p>Pick a target multiplier and bet. A result is drawn; if it reaches your target, you win your stake times the target. The higher the target, the rarer the win.</p>
    <div class="facts"><div><b>5,000×</b><small>Max target</small></div><div><b>1.01×</b><small>Lowest target</small></div></div>
    <h3>How it pays</h3>
    <p>The higher the target, the less often it is reached: the chance is shown as you set it. Every target returns the same.</p>
    <h3>Fairness</h3>
    <p>The result is floor(R ÷ (1 − u)) ÷ 100, with R the game's return rate (at least 1.00×, at most 5,000×), with u the first float of HMAC-SHA256(server seed, "client seed:nonce:0"). Change seeds to check any round. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);

  const tweens = new Tweens(app.ticker);
  const shade = new Graphics();
  const comet = new Sprite(tex('comet.webp'));
  comet.anchor.set(0.5);
  comet.blendMode = 'add';
  const big = new Text({ text: '1.00×', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: 90, fill: 0xffffff, stroke: { color: 0x1a0b2a, width: 7 }, dropShadow: { color: 0xffb02e, alpha: 0.85, blur: 24, distance: 0 }, padding: 48 } });
  big.anchor.set(0.5);
  const goal = new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '800', fontSize: 18, fill: 0xffe1a6, dropShadow: { color: 0, alpha: 0.7, blur: 8, distance: 0 } } });
  goal.anchor.set(0.5);
  app.stage.addChild(shade, comet, big, goal);
  let W = 0, H = 0;
  const layout = () => {
    W = app.screen.width; H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x05030c, alpha: 0.25 });
    big.style.fontSize = Math.round(Math.min(W, H) * 0.17);
    big.position.set(W / 2, H * 0.45);
    goal.position.set(W / 2, big.y + big.height * 0.65);
    comet.scale.set((Math.min(W, H) * 0.34) / comet.texture.width);
    comet.position.set(W * 0.2, H * 0.85);
  };
  layout();
  app.renderer.on('resize', layout);
  set(target);
  let glow = 0;
  app.ticker.add((t: Ticker) => { glow += t.deltaTime * 0.05; comet.alpha = 0.85 + Math.sin(glow) * 0.15; });

  shell.action.textContent = 'BET';
  shell.action.onclick = async () => {
    if (busy) return;
    busy = true;
    shell.action.disabled = true;
    shell.lock(true);
    sound.spin();
    big.style.fill = 0xffffff;
    try {
      const v = await call<LimboView>('instant/limbo/play', { stake: shell.stake, choices: { target }, key: key() });
      // The comet streaks up while the number climbs to the result (faster for small results).
      const end = v.detail.result / 10000, ms = 500 + Math.min(1100, Math.log(end) * 260);
      await tweens.to(0, 1, ms, (k) => {
        big.text = times(Math.floor(Math.exp(Math.log(end) * k) * 100) * 100);
        comet.position.set(W * (0.2 + 0.62 * k), H * (0.85 - 0.7 * k));
        comet.rotation = -0.2 * k;
      }, ease.outCubic);
      big.text = times(v.detail.result);
      big.style.fill = v.detail.win ? 0x6dffc4 : 0xff6b8b;
      shell.setBalance(v.balance);
      shell.setWin(v.win);
      if (v.detail.win) { sound.win(target >= 50000 ? 3 : 1); if (target >= 100000) shell.bigWin(v.win, target); } else sound.stop(0);
      void tweens.to(1, 0, 700, (k) => comet.position.set(W * (0.2 + 0.62 * k), H * (0.85 - 0.7 * k)), ease.inOutSine);
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
