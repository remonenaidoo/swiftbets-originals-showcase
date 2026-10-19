import { imageArt, startSlot } from '@originals/slot-kit';

const symbol = (file: string) => `assets/karoo-stargazer/symbols/${file}.webp`;

startSlot({
  gameId: 'karoo-stargazer',
  title: 'Karoo Stargazer',
  art: imageArt({
    Leopard: symbol('leopard'), Kudu: symbol('kudu'), Meerkat: symbol('meerkat'), Ostrich: symbol('ostrich'),
    Ace: symbol('ace'), King: symbol('king'), Queen: symbol('queen'), Jack: symbol('jack'), Ten: symbol('ten'),
    Wild: symbol('wild'), Scatter: symbol('scatter'),
  }),
  background: { portrait: 'assets/karoo-stargazer/bg-portrait.jpg', landscape: 'assets/karoo-stargazer/bg-landscape.jpg' },
  logo: 'assets/karoo-stargazer/logo.webp',
  colours: { background: 0x05071a, backgroundTop: 0x111a44, frame: 0xc9d6ff, accent: 0xffd27a, featureTint: 0x2a1a5e },
  rules: (info, _stake, img) => `
    <h3>Wild and Scatter</h3>
    <p>${img('Wild')}</p>
    <p><b>Southern Cross Wild</b> appears on reels 2, 3 and 4 and stands in for every symbol except the Scatter.</p>
    <p><b>Observatory Scatter</b> pays anywhere on the reels, multiplied by the total stake. 3 or more award <b>${info.freeSpins} free spins</b>.</p>
    <h3>Free spins</h3>
    <p>Free spins are played on their own reels at the stake that triggered them. Every free-spin win is multiplied by <b>×${info.freeSpinMultiplier}</b>. 3 or more Scatters during free spins award ${info.freeSpins} more.</p>`,
});
