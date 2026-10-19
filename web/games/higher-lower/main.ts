import { Container, Graphics, Text } from 'pixi.js';
import { rand, sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times, floats, cardFace, cardBack, RANKS, SUITS } from '@originals/instant-kit';

interface HlView { roundId: string; stake: number; cards: number[]; guesses: number[]; multiplierBp: number; higherBp: number; lowerBp: number; win: number; status: string; balance: number | null }

const SKIP = 0, HIGHER = 1, LOWER = 2;
const name = (c: number) => `${RANKS[c % 13]}${SUITS[Math.floor(c / 13)]}`;

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant<Record<string, never>, HlView>({
    gameId: 'higher-lower', title: 'Higher or Lower', accent: '#7cc4ff', accent2: '#3b5bdb', background: '#050a18', art: ['card-back.webp'],
    verify: async (s, c, n) => { const f = await floats(s, c, n, 12); return `the deck began ${f.map((x) => name(Math.floor(x * 52))).join(', ')} …`; },
  });
  let round: HlView | null = state.open, busy = false;

  shell.controls.innerHTML = `
    <div class="row" style="gap:8px">
      <button class="chip" id="g-hi" style="flex:1;padding:12px 6px">▲ Higher or same<br><b id="hi-x"></b></button>
      <button class="chip" id="g-lo" style="flex:1;padding:12px 6px">▼ Lower or same<br><b id="lo-x"></b></button>
      <button class="chip" id="g-skip" style="flex:0 0 auto;padding:12px 14px">Skip ⟳</button>
    </div>`;
  const hi = document.getElementById('g-hi') as HTMLButtonElement, lo = document.getElementById('g-lo') as HTMLButtonElement, skip = document.getElementById('g-skip') as HTMLButtonElement;

  shell.rules(`
    <h2>Higher or Lower</h2>
    <p>A card is turned. Guess whether the next one is higher or the same, or lower or the same, or skip it. Every right guess raises your multiplier; cash out whenever you like. A wrong guess loses the round.</p>
    <div class="facts"><div><b>5,000×</b><small>Max win</small></div><div><b>A low · K high</b><small>Ranks</small></div></div>
    <h3>How it pays</h3>
    <p>A guess that wins with c of the 13 ranks multiplies by 13 ÷ c: the riskier the guess, the more it adds. The house edge is taken once, on your first guess, so wherever you cash out the game returns the same. A guess that cannot lose is not offered, nor one whose win would pass 5,000×.</p>
    <h3>Fairness</h3>
    <p>The deck is drawn from your seeds before you play: card k is float k × 52, floored (rank = card mod 13 + 1). Only cards already turned are ever sent to your device. An unfinished round is kept: reopen the game to carry on. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);

  // ---- Stage ----
  const tweens = new Tweens(app.ticker);
  const shade = new Graphics(), trail = new Container(), main = new Container(), deck = new Container();
  const mult = new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: 30, fill: 0xffffff, stroke: { color: 0x050a18, width: 5 }, dropShadow: { color: 0x7cc4ff, alpha: 0.7, blur: 14, distance: 0 }, padding: 22 } });
  mult.anchor.set(0.5);
  app.stage.addChild(shade, trail, deck, main, mult);
  let cw = 100, CX = 0, CY = 0;

  const draw = () => {
    const W = app.screen.width, H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x02050c, alpha: 0.3 });
    cw = Math.min(W * 0.3, (H - 130) / 1.4);
    CX = W / 2; CY = 14 + (H - 60) / 2 + 12;
    main.removeChildren().forEach((c) => c.destroy());
    deck.removeChildren().forEach((c) => c.destroy());
    trail.removeChildren().forEach((c) => c.destroy());
    const current = round?.cards[round.cards.length - 1];
    if (current !== undefined) main.addChild(cardFace(current, cw)); else main.addChild(cardBack(tex('card-back.webp'), cw));
    main.position.set(CX, CY);
    const back = cardBack(tex('card-back.webp'), cw * 0.62);
    back.position.set(CX + cw * 0.95, CY);
    deck.addChild(back);
    // The cards already played, small, along the top: dimmed for skips, red for a lost guess.
    const past = round ? round.cards.slice(0, -1) : [];
    const small = Math.min(36, (W - 20) / Math.max(8, past.length + 1));
    past.slice(-14).forEach((c, i, arr) => {
      const f = cardFace(c, small);
      f.position.set(CX - ((arr.length - 1) / 2 - i) * small * 1.08, 12 + small * 0.7);
      const g = round!.guesses[past.length - arr.length + i];
      f.alpha = g === SKIP ? 0.45 : 1;
      trail.addChild(f);
      if (g !== SKIP) trail.addChild(Object.assign(new Text({ text: g === HIGHER ? '▲' : '▼', style: { fontSize: small * 0.4, fill: 0x7cc4ff, fontWeight: '900' } }), { x: f.x - small * 0.15, y: f.y + small * 0.72 }));
    });
    mult.position.set(CX, Math.min(H - 20, CY + cw * 0.7 + 30));
    mult.text = round ? (round.status === 'busted' ? 'Wrong guess' : round.multiplierBp > 10000 || round.guesses.some((g) => g !== SKIP) ? times(round.multiplierBp) : '') : '';
    mult.style.fill = round?.status === 'busted' ? 0xff8a9a : 0xffffff;
  };
  app.renderer.on('resize', draw);

  const show = () => {
    const open = round?.status === 'open';
    // The shell locks every control during a round; the guess buttons are set after, by what this card allows.
    shell.lock(!!open || busy);
    hi.disabled = !open || busy || !round!.higherBp;
    lo.disabled = !open || busy || !round!.lowerBp;
    skip.disabled = !open || busy;
    document.getElementById('hi-x')!.textContent = open && round!.higherBp ? times(round!.higherBp) : '—';
    document.getElementById('lo-x')!.textContent = open && round!.lowerBp ? times(round!.lowerBp) : '—';
    const a = shell.action;
    if (open && round!.multiplierBp > 0 && round!.guesses.some((g) => g !== SKIP)) {
      a.className = 'action cash';
      a.innerHTML = `CASH OUT<small>${rand(Math.floor((round!.stake * round!.multiplierBp) / 10000))} · ${times(round!.multiplierBp)}</small>`;
      a.disabled = busy;
    } else if (open) {
      a.className = 'action';
      a.innerHTML = 'MAKE A GUESS';
      a.disabled = true;
    } else {
      a.className = 'action';
      a.innerHTML = 'BET';
      a.disabled = busy;
    }
  };

  const finish = (v: HlView) => {
    shell.setBalance(v.balance);
    shell.setWin(v.win);
    if (v.status === 'cashed') { sound.win(v.multiplierBp >= 50000 ? 3 : 1); if (v.multiplierBp >= 100000) shell.bigWin(v.win, v.multiplierBp); }
    void refresh();
  };

  const guess = async (g: number) => {
    if (busy || round?.status !== 'open') return;
    busy = true;
    show();
    try {
      const v = await call<HlView>('instant/higher-lower/guess', { roundId: round.roundId, guess: g });
      // The next card slides off the deck and turns over onto the pile.
      const next = v.cards[v.cards.length - 1];
      const moving = new Container();
      const back = cardBack(tex('card-back.webp'), cw), face = cardFace(next, cw);
      face.visible = false;
      moving.addChild(back, face);
      moving.position.set(CX + cw * 0.95, CY);
      moving.scale.set(0.62);
      app.stage.addChild(moving);
      await tweens.to(0, 1, 240, (k) => { moving.x = CX + cw * 0.95 * (1 - k); moving.scale.set(0.62 + 0.38 * k); }, ease.outCubic);
      await tweens.to(1, 0, 100, (k) => { moving.scale.x = k; });
      back.visible = false; face.visible = true;
      await tweens.to(0, 1, 140, (k) => { moving.scale.x = k; }, ease.outBack);
      moving.destroy();
      round = v;
      draw();
      if (v.status === 'busted') { sound.stop(0); for (let s = 0; s < 6; s++) { main.x = CX + (Math.random() - 0.5) * 12; await tweens.wait(30); } main.x = CX; }
      else if (g !== SKIP) sound.scatter(Math.min(v.guesses.filter((x) => x !== SKIP).length, 6));
      else sound.tick();
      if (v.status !== 'open') finish(v);
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      busy = false;
      show();
    }
  };
  hi.onclick = () => void guess(HIGHER);
  lo.onclick = () => void guess(LOWER);
  skip.onclick = () => void guess(SKIP);

  shell.action.onclick = async () => {
    if (busy) return;
    busy = true;
    show();
    try {
      if (round?.status === 'open') { round = await call<HlView>('instant/higher-lower/cashout', { roundId: round.roundId }); draw(); finish(round); }
      else {
        round = await call<HlView>('instant/higher-lower/start', { stake: shell.stake, key: key() });
        shell.setBalance(round.balance);
        shell.setWin(0);
        sound.spin();
        draw();
      }
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      busy = false;
      show();
    }
  };

  draw();
  show();
};

boot().catch((e) => { document.body.innerHTML = `<p style="color:#eee;font-family:system-ui;padding:24px">${describe(e)}</p>`; });
