import { Container, Graphics, Text } from 'pixi.js';
import { sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times, floats, cardFace, cardBack, RANKS, SUITS } from '@originals/instant-kit';

type Bet = 'player' | 'banker' | 'tie';
interface BacView { roundId: string; multiplierBp: number; win: number; detail: { bet: Bet; player: number[]; banker: number[]; winner: Bet; playerTotal: number; bankerTotal: number }; balance: number | null }
interface BacInfo { rules: { pays: Record<Bet, number>; rtp: Record<Bet, number> } }

const value = (card: number) => { const r = (card % 13) + 1; return r >= 10 ? 0 : r; };
const LABEL: Record<Bet, string> = { player: 'Player', banker: 'Banker', tie: 'Tie' };

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant<BacInfo>({
    gameId: 'baccarat', title: 'Baccarat', accent: '#e8c35a', accent2: '#1c8a55', background: '#04120a', art: ['card-back.webp'],
    verify: async (s, c, n) => { const f = await floats(s, c, n, 6); return `the six cards in dealing order were ${f.map((x) => { const id = Math.floor(x * 52); return `${RANKS[id % 13]}${SUITS[Math.floor(id / 13)]}`; }).join(', ')} (Player, Banker, Player, Banker, then each third card if drawn).`; },
  });
  const rules = state.info.rules;
  let bet: Bet = 'player', busy = false;

  shell.controls.innerHTML = `<div class="row"><div class="label">Bet on</div><div class="chips" id="bets">${(['player', 'banker', 'tie'] as Bet[]).map((b) => `<button class="chip${b === bet ? ' on' : ''}" data-v="${b}">${LABEL[b]} <small style="opacity:.7">${times(rules.pays[b])}</small></button>`).join('')}</div></div>`;
  for (const b of document.querySelectorAll<HTMLElement>('#bets .chip')) b.onclick = () => { if (busy) return; bet = b.dataset.v as Bet; for (const x of document.querySelectorAll('#bets .chip')) x.classList.toggle('on', x === b); layout(); };

  shell.rules(`
    <h2>Baccarat</h2>
    <p>Bet on the Player hand, the Banker hand, or a tie. Each hand gets two cards; the one closest to 9 wins. Cards count at face value, Aces 1, tens and pictures 0, and only the last digit of a total counts (7 + 8 = 5).</p>
    <div class="facts"><div><b>${rules.rtp.banker.toFixed(2)}%</b><small>Banker RTP</small></div><div><b>${rules.rtp.player.toFixed(2)}%</b><small>Player RTP</small></div><div><b>${rules.rtp.tie.toFixed(2)}%</b><small>Tie RTP</small></div></div>
    <h3>Pays</h3>
    <table class="table"><tr><th>Bet</th><th>Pays</th><th>RTP</th></tr>${(['player', 'banker', 'tie'] as Bet[]).map((b) => `<tr><td>${LABEL[b]}</td><td>${b === 'player' ? '1 to 1' : b === 'banker' ? '0.95 to 1' : '8 to 1'}</td><td>${rules.rtp[b].toFixed(2)}%</td></tr>`).join('')}</table>
    <p>On a tie, Player and Banker bets are returned.</p>
    <h3>Third cards</h3>
    <p>If either hand has 8 or 9 with its first two cards (a natural), both stand. Otherwise the Player draws a third card on 0–5 and stands on 6–7. If the Player stood, the Banker draws on 0–5. If the Player drew, the Banker draws on 0–2; on 3 unless the Player's third card was 8; on 4 if it was 2–7; on 5 if it was 4–7; on 6 if it was 6 or 7; and stands on 7.</p>
    <h3>Fairness</h3>
    <p>Cards come from an endless shoe: each is one float × 52, floored, dealt Player, Banker, Player, Banker, then the third cards. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);

  // ---- Stage ----
  const tweens = new Tweens(app.ticker);
  const shade = new Graphics(), zones = new Graphics(), cards = new Container(), texts = new Container();
  const mk = (size: number, fill: number) => new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: size, fill, stroke: { color: 0x04120a, width: 4 }, padding: 12 } });
  const pName = mk(18, 0x9fd8ff), bName = mk(18, 0xffb3b3), pTotal = mk(30, 0xffffff), bTotal = mk(30, 0xffffff), banner = mk(30, 0xffe58a);
  for (const t of [pName, bName, pTotal, bTotal, banner]) { t.anchor.set(0.5); texts.addChild(t); }
  pName.text = 'PLAYER'; bName.text = 'BANKER';
  app.stage.addChild(shade, zones, cards, texts);
  let cw = 60, px = 0, bx = 0, cy = 0;

  const layout = () => {
    const W = app.screen.width, H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x020a05, alpha: 0.3 });
    cw = Math.min(W / 9.5, (H - 120) / 1.9);
    px = W * 0.27; bx = W * 0.73; cy = 34 + (H - 90) / 2;
    zones.clear();
    for (const [x, b] of [[px, 'player'], [bx, 'banker']] as [number, Bet][]) {
      const on = bet === b;
      zones.roundRect(x - cw * 1.9, cy - cw * 1.05, cw * 3.8, cw * 2.1, 16).fill({ color: on ? 0x0f3a24 : 0x082416, alpha: 0.75 }).roundRect(x - cw * 1.9, cy - cw * 1.05, cw * 3.8, cw * 2.1, 16).stroke({ width: on ? 3 : 1.5, color: on ? 0xe8c35a : 0x2f6b4c });
    }
    pName.position.set(px, cy - cw * 1.05 - 16); bName.position.set(bx, cy - cw * 1.05 - 16);
    pTotal.position.set(px, cy + cw * 1.05 + 22); bTotal.position.set(bx, cy + cw * 1.05 + 22);
    banner.position.set(W / 2, Math.min(H - 20, cy + cw * 1.05 + 58));
  };
  app.renderer.on('resize', layout);
  layout();

  /** Deals a card face down from the shoe to its place (a hand of n cards is centred on x), then turns it. */
  const deal = async (card: number, x: number, slot: number, n: number) => {
    const holder = new Container();
    holder.position.set(app.screen.width / 2, -cw);
    const back = cardBack(tex('card-back.webp'), cw), face = cardFace(card, cw);
    face.visible = false;
    holder.addChild(back, face);
    cards.addChild(holder);
    const tx = x + (slot - (n - 1) / 2) * cw * 1.1, ty = cy;
    await tweens.to(0, 1, 280, (k) => { holder.x = app.screen.width / 2 + (tx - app.screen.width / 2) * k; holder.y = -cw + (ty + cw) * k; }, ease.outCubic);
    await tweens.to(1, 0, 110, (k) => holder.scale.set(k, 1));
    back.visible = false; face.visible = true;
    await tweens.to(0, 1, 140, (k) => holder.scale.set(k, 1), ease.outBack);
    sound.tick();
  };

  shell.action.textContent = 'DEAL';
  shell.action.onclick = async () => {
    if (busy) return;
    busy = true;
    shell.action.disabled = true;
    shell.lock(true);
    cards.removeChildren().forEach((c) => c.destroy());
    pTotal.text = bTotal.text = banner.text = '';
    sound.spin();
    try {
      const v = await call<BacView>('instant/baccarat/play', { stake: shell.stake, choices: { bet }, key: key() });
      const { player, banker } = v.detail;
      const total = (h: number[]) => h.reduce((a, c) => a + value(c), 0) % 10;
      const [np, nb] = [player.length, banker.length];
      await deal(player[0], px, 0, np); pTotal.text = String(total(player.slice(0, 1)));
      await deal(banker[0], bx, 0, nb); bTotal.text = String(total(banker.slice(0, 1)));
      await deal(player[1], px, 1, np); pTotal.text = String(total(player.slice(0, 2)));
      await deal(banker[1], bx, 1, nb); bTotal.text = String(total(banker.slice(0, 2)));
      if (np === 3) { await tweens.wait(250); await deal(player[2], px, 2, np); pTotal.text = String(total(player)); }
      if (nb === 3) { await tweens.wait(250); await deal(banker[2], bx, 2, nb); bTotal.text = String(total(banker)); }
      const w = v.detail.winner;
      banner.text = w === 'tie' ? `Tie ${v.detail.playerTotal}–${v.detail.bankerTotal}` : `${LABEL[w]} wins ${Math.max(v.detail.playerTotal, v.detail.bankerTotal)} to ${Math.min(v.detail.playerTotal, v.detail.bankerTotal)}`;
      banner.style.fill = w === bet ? 0xffe58a : v.multiplierBp ? 0xd8e6dc : 0xffa3a3;
      shell.setBalance(v.balance);
      shell.setWin(v.win);
      if (w === bet) { sound.win(bet === 'tie' ? 3 : 1); if (v.multiplierBp >= 90000) shell.bigWin(v.win, v.multiplierBp); } else if (!v.multiplierBp) sound.stop(0);
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
