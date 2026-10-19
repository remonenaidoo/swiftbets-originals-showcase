import { rand } from '@originals/slot-kit';
import { startRoundSlot } from '@originals/instant-kit/round-slot';

const sym = (n: string) => `assets/cape-point-ways/symbols/${n}.webp`;

void startRoundSlot({
  gameId: 'cape-point-ways',
  title: 'Cape Point Ways',
  mechanic: 'ways',
  symbols: {
    Shark: sym('shark'), Whale: sym('whale'), Lighthouse: sym('lighthouse'), Penguin: sym('penguin'), Anchor: sym('anchor'), Compass: sym('compass'),
    Shell: sym('shell'), Starfish: sym('starfish'), Wild: sym('wild'), Wild2: sym('wild2'), Wild3: sym('wild3'), Pearl: sym('pearl'),
  },
  background: { portrait: 'assets/cape-point-ways/bg-portrait.jpg', landscape: 'assets/cape-point-ways/bg-landscape.jpg' },
  logo: 'assets/cape-point-ways/logo.webp',
  colours: { background: 0x04161d, backgroundTop: 0x0b3646, frame: 0x47e0d0, accent: 0xfff1b8, featureTint: 0x0a4a66 },
  css: { '--gold': '#7cf0e0', '--gold-2': '#2aa8a0', '--teal': '#ffd27a', '--panel': 'rgba(4, 22, 30, 0.88)', '--line': 'rgba(124, 240, 224, 0.35)' },
  rules: (info, stake, img) => {
    const unit = (stake / info.units) * (info.rateBp / 9600);
    const pays = Object.entries(info.pays as Record<string, number[]>).map(([s, p]) =>
      `<div class="pay"><div class="pics">${img(s)}</div><div class="grid">${[5, 4, 3].map((n) => `<div><small>${n}</small><b>${rand(p[n - 3] * unit)}</b></div>`).join('')}</div></div>`).join('');
    return `
      <h2>Cape Point Ways</h2>
      <p>5 reels, 3 rows, 243 ways to win by SwiftBets Originals. A symbol pays when it shows on adjacent reels from reel 1, anywhere on each reel: every combination of its positions is a way.</p>
      <div class="facts"><div><b>${Math.floor((5000 * info.rateBp) / 9600).toLocaleString('en-ZA')}×</b><small>Max win</small></div><div><b>243</b><small>Ways</small></div></div>
      <h3>Pays per way at ${rand(stake)} stake</h3>
      <div class="pays">${pays}</div>
      <h3>Wilds</h3>
      <p>${img('Wild')} The Wave lands on reels 2, 3 and 4 and stands in for every symbol except the Pearl. In free spins Waves can be ${img('Wild2')} ×2 or ${img('Wild3')} ×3: every way through them is multiplied (two ×3 Waves on one way make ×9).</p>
      <h3>Pearls and free spins</h3>
      <p>${img('Pearl')} 3, 4 or 5 Pearls anywhere pay ${info.scatterStakes.map((s: number) => `${s}×`).join(', ')} the stake and award ${info.freeSpins.join(', ')} free spins, played at the stake that won them.</p>
      <h3>How wins pay</h3>
      <p>Each symbol's ways are added; Pearl wins are added. A round (a spin and its free spins) pays at most 5,000× the stake. Every round is played in full on the server from your provably fair seeds before it is shown.</p>
      <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`;
  },
});
