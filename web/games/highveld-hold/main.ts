import { rand } from '@originals/slot-kit';
import { startRoundSlot } from '@originals/instant-kit/round-slot';

const sym = (n: string) => `assets/highveld-hold/symbols/${n}.webp`;

void startRoundSlot({
  gameId: 'highveld-hold',
  title: 'Highveld Hold',
  mechanic: 'hold',
  symbols: {
    Thunderbird: sym('thunderbird'), Windpomp: sym('windpomp'), Springbok: sym('springbok'), Cosmos: sym('cosmos'), Ruby: sym('ruby'), Sapphire: sym('sapphire'),
    Emerald: sym('emerald'), Amber: sym('amber'), Wild: sym('wild'), Coin: sym('coin'), Blank: sym('blank'),
  },
  background: { portrait: 'assets/highveld-hold/bg-portrait.jpg', landscape: 'assets/highveld-hold/bg-landscape.jpg' },
  logo: 'assets/highveld-hold/logo.webp',
  colours: { background: 0x0d0618, backgroundTop: 0x2a1450, frame: 0xffc94a, accent: 0xd8b4ff, featureTint: 0x3a1670 },
  css: { '--gold': '#ffc94a', '--gold-2': '#c7861f', '--teal': '#c99bff', '--panel': 'rgba(16, 8, 30, 0.88)', '--line': 'rgba(255, 201, 74, 0.35)' },
  rules: (info, stake, img) => {
    const lineBet = stake / info.units;
    const pays = Object.entries(info.pays as Record<string, number[]>).map(([s, p]) =>
      `<div class="pay"><div class="pics">${img(s)}</div><div class="grid">${[5, 4, 3].map((n) => `<div><small>${n}</small><b>${rand(p[n - 3] * lineBet)}</b></div>`).join('')}</div></div>`).join('');
    const values = (info.values as { stakes: number; weight: number }[]).map((v) => ({ 15: 'Mini', 40: 'Minor', 150: 'Major' } as Record<number, string>)[v.stakes] ? `${({ 15: 'Mini', 40: 'Minor', 150: 'Major' } as Record<number, string>)[v.stakes]} ${rand(v.stakes * stake)}` : rand(v.stakes * stake)).join(' · ');
    return `
      <h2>Highveld Hold</h2>
      <p>5 reels, 3 rows, 20 fixed lines by SwiftBets Originals, with a hold-and-win feature.</p>
      <div class="facts"><div><b>${info.rtp.toFixed(2)}%</b><small>Return to player</small></div><div><b>5,000×</b><small>Max win</small></div><div><b>Grand ${rand(info.grand * stake)}</b><small>Full board</small></div></div>
      <h3>Line pays at ${rand(stake)} stake</h3>
      <div class="pays">${pays}</div>
      <p>${img('Wild')} The Storm lands on reels 2, 3 and 4 and stands in for every symbol except coins.</p>
      <h3>Hold and win</h3>
      <p>${img('Coin')} Coins carry a value: ${values}. Six or more coins start the hold: they stay in place and you get 3 respins, in which only coins can land. Every new coin stays too and resets the respins to 3. Fill all 15 cells for the Grand (${rand(info.grand * stake)}). When the respins run out, every coin on the board pays.</p>
      <h3>How wins pay</h3>
      <p>Line wins pay the highest win per line, left to right from reel 1; coins do not pay on lines. A round pays at most 5,000× the stake. Every round is played in full on the server from your provably fair seeds before it is shown.</p>
      <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`;
  },
});
