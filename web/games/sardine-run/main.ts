import { rand } from '@originals/slot-kit';
import { startRoundSlot } from '@originals/instant-kit/round-slot';

const sym = (n: string) => `assets/sardine-run/symbols/${n}.webp`;

// Cape Point Ways' engine under a new theme: the engine's symbol names map to this game's art and names.
const NAMES: Record<string, string> = {
  Shark: 'Dolphin', Whale: 'Gannet', Lighthouse: 'Seal', Penguin: 'Sardine Shoal', Anchor: 'Hook', Compass: 'Buoy', Shell: 'Mussel', Starfish: 'Crab',
};

void startRoundSlot({
  gameId: 'sardine-run',
  title: 'Sardine Run',
  mechanic: 'ways',
  symbols: {
    Shark: sym('dolphin'), Whale: sym('gannet'), Lighthouse: sym('seal'), Penguin: sym('shoal'), Anchor: sym('hook'), Compass: sym('buoy'),
    Shell: sym('mussel'), Starfish: sym('crab'), Wild: sym('wild'), Wild2: sym('wild2'), Wild3: sym('wild3'), Pearl: sym('scatter'),
  },
  background: { portrait: 'assets/sardine-run/bg-portrait.jpg', landscape: 'assets/sardine-run/bg-landscape.jpg' },
  logo: 'assets/sardine-run/logo.webp',
  colours: { background: 0x031a24, backgroundTop: 0x0a3a4e, frame: 0x5ee6ff, accent: 0xfff3c4, featureTint: 0x0b4e6e },
  css: { '--gold': '#7ce8ff', '--gold-2': '#2a9cc0', '--teal': '#ffd98a', '--panel': 'rgba(3, 24, 34, 0.88)', '--line': 'rgba(124, 232, 255, 0.35)' },
  rules: (info, stake, img) => {
    const unit = stake / info.units;
    const pays = Object.entries(info.pays as Record<string, number[]>).map(([s, p]) =>
      `<div class="pay"><div class="pics">${img(s)}</div><div class="grid">${[5, 4, 3].map((n) => `<div><small>${n}</small><b>${rand(p[n - 3] * unit)}</b></div>`).join('')}</div></div>`).join('');
    return `
      <h2>Sardine Run</h2>
      <p>5 reels, 3 rows, 243 ways to win by SwiftBets Originals. A symbol pays when it shows on adjacent reels from reel 1, anywhere on each reel: every combination of its positions is a way.</p>
      <div class="facts"><div><b>${info.rtp.toFixed(2)}%</b><small>Return to player</small></div><div><b>5,000×</b><small>Max win</small></div><div><b>243</b><small>Ways</small></div></div>
      <h3>Pays per way at ${rand(stake)} stake</h3>
      <p>${Object.values(NAMES).join(' · ')}, highest to lowest.</p>
      <div class="pays">${pays}</div>
      <h3>Wilds</h3>
      <p>${img('Wild')} The Wave lands on reels 2, 3 and 4 and stands in for every symbol except Hole in the Wall. In free spins Waves can be ${img('Wild2')} ×2 or ${img('Wild3')} ×3: every way through them is multiplied (two ×3 Waves on one way make ×9).</p>
      <h3>Hole in the Wall and free spins</h3>
      <p>${img('Pearl')} 3, 4 or 5 Hole in the Wall symbols anywhere pay ${info.scatterStakes.map((s: number) => `${s}×`).join(', ')} the stake and award ${info.freeSpins.join(', ')} free spins, played at the stake that won them.</p>
      <h3>How wins pay</h3>
      <p>Each symbol's ways are added; Hole in the Wall wins are added. A round (a spin and its free spins) pays at most 5,000× the stake. Every round is played in full on the server from your provably fair seeds before it is shown.</p>
      <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`;
  },
});
