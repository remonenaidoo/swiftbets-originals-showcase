import { rand } from '@originals/slot-kit';
import { startRoundSlot } from '@originals/instant-kit/round-slot';

// Highveld Hold's engine under a new theme: the engine's symbol names map to this game's art.
const sym = (n: string) => `assets/kimberley-hold/symbols/${n}.webp`;

void startRoundSlot({
  gameId: 'kimberley-hold',
  title: 'Kimberley Hold',
  mechanic: 'hold',
  symbols: {
    Thunderbird: sym('prospector'), Windpomp: sym('headgear'), Springbok: sym('mule'), Cosmos: sym('lantern'), Ruby: sym('pink'), Sapphire: sym('blue'),
    Emerald: sym('green'), Amber: sym('yellow'), Wild: sym('wild'), Coin: sym('coin'), Blank: sym('blank'),
  },
  background: { portrait: 'assets/kimberley-hold/bg-portrait.jpg', landscape: 'assets/kimberley-hold/bg-landscape.jpg' },
  logo: 'assets/kimberley-hold/logo.webp',
  colours: { background: 0x120904, backgroundTop: 0x3a2210, frame: 0xffd27a, accent: 0xbfe9ff, featureTint: 0x4a2a0e },
  css: { '--gold': '#ffd27a', '--gold-2': '#c08a3a', '--teal': '#a9e4ff', '--panel': 'rgba(22, 12, 4, 0.88)', '--line': 'rgba(255, 210, 122, 0.35)' },
  rules: (info, stake, img) => {
    const lineBet = stake / info.units;
    const pays = Object.entries(info.pays as Record<string, number[]>).map(([s, p]) =>
      `<div class="pay"><div class="pics">${img(s)}</div><div class="grid">${[5, 4, 3].map((n) => `<div><small>${n}</small><b>${rand(p[n - 3] * lineBet)}</b></div>`).join('')}</div></div>`).join('');
    const values = (info.values as { stakes: number; weight: number }[]).map((v) => ({ 15: 'Mini', 40: 'Minor', 150: 'Major' } as Record<number, string>)[v.stakes] ? `${({ 15: 'Mini', 40: 'Minor', 150: 'Major' } as Record<number, string>)[v.stakes]} ${rand(v.stakes * stake)}` : rand(v.stakes * stake)).join(' · ');
    return `
      <h2>Kimberley Hold</h2>
      <p>5 reels, 3 rows, 20 fixed lines by SwiftBets Originals, with a hold-and-win feature.</p>
      <div class="facts"><div><b>${info.rtp.toFixed(2)}%</b><small>Return to player</small></div><div><b>5,000×</b><small>Max win</small></div><div><b>Grand ${rand(info.grand * stake)}</b><small>Full board</small></div></div>
      <h3>Line pays at ${rand(stake)} stake</h3>
      <div class="pays">${pays}</div>
      <p>${img('Wild')} The Big Hole lands on reels 2, 3 and 4 and stands in for every symbol except diamonds.</p>
      <h3>Hold and win</h3>
      <p>${img('Coin')} Diamonds carry a value: ${values}. Six or more diamonds start the hold: they stay in place and you get 3 respins, in which only diamonds can land. Every new diamond stays too and resets the respins to 3. Fill all 15 cells for the Grand (${rand(info.grand * stake)}). When the respins run out, every diamond on the board pays.</p>
      <h3>How wins pay</h3>
      <p>Line wins pay the highest win per line, left to right from reel 1; diamonds do not pay on lines. A round pays at most 5,000× the stake. Every round is played in full on the server from your provably fair seeds before it is shown.</p>
      <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`;
  },
});
