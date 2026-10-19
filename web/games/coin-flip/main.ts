import { Container, Graphics, Sprite, Text, type Ticker } from 'pixi.js';
import { rand, sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times, floats } from '@originals/instant-kit';

interface FlipView { roundId: string; multiplierBp: number; win: number; detail: { coins: number; need: number; side: string; flips: string[]; landed: number; win: boolean }; balance: number | null }

/** At least `need` of `coins` on one side: the number of the 2^coins outcomes, and the pay (the game's rate ÷ P, floored to 0.0001×). */
let rateBp = 9600;
const ways = (coins: number, need: number) => { let w = 0, c = 1; for (let k = 0; k <= coins; k++) { if (k >= need) w += c; c = (c * (coins - k)) / (k + 1); } return w; };
const payBp = (coins: number, need: number) => Math.floor((rateBp * 2 ** coins) / ways(coins, need));

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant({
    gameId: 'coin-flip', title: 'Coin Flip', accent: '#ffd25e', accent2: '#1fb5a6', background: '#04161a', art: ['heads.webp', 'tails.webp'],
    verify: async (s, c, n) => { const f = await floats(s, c, n, coins); return `the coins landed ${f.map((x) => (x < 0.5 ? 'heads' : 'tails')).join(', ')} (for ${coins} coins).`; },
  });
  rateBp = state.info.rateBp;
  let coins = 1, need = 1, side: 'heads' | 'tails' = 'heads', busy = false;
  let faces: string[] = [];

  shell.controls.innerHTML = `
    <div class="row"><div class="label">Side</div><div class="chips" id="side"><button class="chip on" data-v="heads">Heads</button><button class="chip" data-v="tails">Tails</button></div></div>
    <div class="row"><div class="label">Coins <b id="coins-l">1</b></div><input id="coins" type="range" min="1" max="10" step="1" value="1" style="flex:1;accent-color:#ffd25e" aria-label="Coins"></div>
    <div class="row"><div class="label">At least <b id="need-l">1</b> on your side</div><input id="need" type="range" min="1" max="1" step="1" value="1" style="flex:1;accent-color:#1fb5a6" aria-label="At least"></div>
    <div class="row" style="font-size:13px;color:var(--muted)"><span>Multiplier <b id="m-out" style="color:var(--ink)"></b></span><span>Win chance <b id="c-out" style="color:var(--ink)"></b></span><span>Pays <b id="p-out" style="color:var(--accent)"></b></span></div>`;
  const cIn = document.getElementById('coins') as HTMLInputElement, nIn = document.getElementById('need') as HTMLInputElement;
  const readout = () => {
    coins = Number(cIn.value);
    nIn.max = String(coins);
    need = Math.min(Number(nIn.value), coins);
    nIn.value = String(need);
    document.getElementById('coins-l')!.textContent = String(coins);
    document.getElementById('need-l')!.textContent = String(need);
    document.getElementById('m-out')!.textContent = times(payBp(coins, need));
    document.getElementById('c-out')!.textContent = `${((ways(coins, need) / 2 ** coins) * 100).toFixed(2)}%`;
    document.getElementById('p-out')!.textContent = rand(Math.floor((shell.stake * payBp(coins, need)) / 10000));
    if (!busy) { faces = Array.from({ length: coins }, () => side); layout(); }
  };
  cIn.oninput = readout; nIn.oninput = readout;
  for (const b of document.querySelectorAll<HTMLElement>('#side .chip')) b.onclick = () => { if (busy) return; side = b.dataset.v as 'heads' | 'tails'; for (const x of document.querySelectorAll('#side .chip')) x.classList.toggle('on', x === b); readout(); };
  for (const id of ['stake-up', 'stake-down']) document.getElementById(id)!.addEventListener('click', () => setTimeout(readout));

  shell.rules(`
    <h2>Coin Flip</h2>
    <p>Pick heads or tails, how many coins to toss (1 to 10), and how many of them must land on your side. The harder the call, the more it pays.</p>
    <div class="facts"><div><b>${times(payBp(10, 10))}</b><small>Ten of ten</small></div><div><b>${times(payBp(1, 1))}</b><small>One coin</small></div></div>
    <h3>How it pays</h3>
    <p>The less likely your call, the more it pays: the multiplier is the game's return rate divided by the chance of at least that many coins landing your side (floored to 0.0001×), shown as you set it.</p>
    <h3>Fairness</h3>
    <p>Each coin is one float of HMAC-SHA256(server seed, "client seed:nonce:0"): below 0.5 is heads. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);

  // ---- Stage ----
  const tweens = new Tweens(app.ticker);
  const shade = new Graphics(), table = new Container();
  const count = new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: 34, fill: 0xffffff, stroke: { color: 0x04161a, width: 5 }, dropShadow: { color: 0x1fb5a6, alpha: 0.7, blur: 14, distance: 0 }, padding: 24 } });
  count.anchor.set(0.5);
  app.stage.addChild(shade, table, count);
  let sprites: Sprite[] = [], size = 80;

  function layout() {
    const W = app.screen.width, H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x021012, alpha: 0.35 });
    const n = faces.length || coins, cols = n <= 3 ? n : n <= 6 ? 3 : n <= 8 ? 4 : 5, rows = Math.ceil(n / cols);
    size = Math.min((W - 40) / cols, (H - 90) / rows) * 0.86;
    if (n === 1) size = Math.min(W, H - 90) * 0.5;
    table.removeChildren().forEach((c) => c.destroy());
    sprites = [];
    for (let i = 0; i < n; i++) {
      const s = new Sprite(tex(`${faces[i] ?? side}.webp`));
      s.anchor.set(0.5);
      s.width = s.height = size;
      const r = Math.floor(i / cols), c = i % cols, inRow = Math.min(cols, n - r * cols);
      s.position.set(W / 2 + (c - (inRow - 1) / 2) * (size / 0.86), 24 + (H - 90) / 2 - ((rows - 1) / 2 - r) * (size / 0.86));
      table.addChild(s);
      sprites.push(s);
    }
    count.position.set(W / 2, H - 34);
  }
  app.renderer.on('resize', layout);
  readout();

  let bob = 0;
  app.ticker.add((t: Ticker) => { if (busy) return; bob += t.deltaTime * 0.04; sprites.forEach((s, i) => { s.y += Math.sin(bob + i) * 0.08; }); });

  shell.action.textContent = 'FLIP';
  shell.action.onclick = async () => {
    if (busy) return;
    busy = true;
    shell.action.disabled = true;
    shell.lock(true);
    count.text = '';
    sound.spin();
    try {
      const v = await call<FlipView>('instant/coin-flip/play', { stake: shell.stake, choices: { coins, need, side }, key: key() });
      // Every coin spins in the air (its width flips between faces), then lands on its result, one after another.
      await Promise.all(sprites.map((s, i) => (async () => {
        const base = s.y, w = size;
        await tweens.to(0, 1, 900 + i * 120, (k) => {
          const turn = Math.cos(k * Math.PI * (8 + i));
          s.width = Math.abs(turn) * w;
          s.texture = tex(`${turn >= 0 ? (k > 0.85 ? v.detail.flips[i] : 'heads') : (k > 0.85 ? v.detail.flips[i] : 'tails')}.webp`);
          s.y = base - Math.sin(k * Math.PI) * size * 0.6;
        }, ease.outCubic);
        s.texture = tex(`${v.detail.flips[i]}.webp`);
        s.width = w; s.y = base;
        s.alpha = v.detail.flips[i] === side ? 1 : 0.4;
        sound.tick();
      })()));
      faces = v.detail.flips;
      count.text = `${v.detail.landed} of ${coins} ${side}${v.detail.win ? ` · ${times(v.multiplierBp)}` : ''}`;
      count.style.fill = v.detail.win ? 0x9dffd8 : 0xffa3b4;
      shell.setBalance(v.balance);
      shell.setWin(v.win);
      if (v.detail.win) { sound.win(v.multiplierBp >= 50000 ? 3 : 1); if (v.multiplierBp >= 100000) shell.bigWin(v.win, v.multiplierBp); } else sound.stop(0);
      void refresh();
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      busy = false;
      shell.action.disabled = false;
      shell.lock(false);
      setTimeout(() => { if (!busy) for (const s of sprites) s.alpha = 1; }, 1600);
    }
  };
};

boot().catch((e) => { document.body.innerHTML = `<p style="color:#eee;font-family:system-ui;padding:24px">${describe(e)}</p>`; });
